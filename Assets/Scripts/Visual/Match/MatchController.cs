using System.Collections.Generic;
using System.Linq;
using Game.Core.Commands;
using Game.Core.Common;
using Game.Core.Events;
using Game.Core.Grid;
using Game.Core.Pieces;
using Game.Core.Pieces.Actions;
using Game.Visual.Grid;
using Game.Visual.Pieces;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Visual.Match
{
    /// <summary>
    /// Bootstrap mínimo do vertical slice do Marco 2: monta o pipeline (bus + processor + contexto),
    /// posiciona uma peça e traduz cliques em seleção/movimento. Demonstra a stack inteira de ponta a
    /// ponta — input → comando → core → evento → visual.
    ///
    /// <para>É o ancestral mínimo do <c>HumanController</c> (Marco 4): ainda sem wizard de seleção em
    /// pilha, turno, stamina ou gate de input durante a animação. Esses entram nos marcos seguintes.</para>
    /// </summary>
    public sealed class MatchController : MonoBehaviour
    {
        [Header("Cena")]
        [SerializeField] private GridRuntime gridRuntime;
        [SerializeField] private GridVisualizer visualizer;
        [SerializeField] private TileHighlighter highlighter;
        [SerializeField] private Camera clickCamera;

        [Header("Peça inicial")]
        [SerializeField] private PieceDefinition pieceDefinition;
        [SerializeField] private GridCoord spawnCoord = new GridCoord(0, 0);
        [SerializeField] private PlayerId owner = PlayerId.Player1;

        private GameEventBus _bus;
        private CommandProcessor _processor;
        private GridSystem _grid;
        private MoveActionDefinition _moveAction;

        private Piece _selected;
        private IReadOnlyList<GridCoord> _selectedDestinations;
        private int _nextPieceId;

        private void Start()
        {
            if (clickCamera == null) clickCamera = Camera.main;

            visualizer.EnsureBuilt();
            gridRuntime.Build();
            _grid = gridRuntime.Grid;

            _bus = new GameEventBus();
            _processor = new CommandProcessor(new GameContext(_grid, _bus));
            if (highlighter != null) highlighter.Bind(_bus);

            _moveAction = pieceDefinition.Actions.OfType<MoveActionDefinition>().FirstOrDefault();
            if (_moveAction == null)
            {
                Debug.LogError($"{nameof(MatchController)}: a PieceDefinition '{pieceDefinition?.DisplayName}' não tem MoveActionDefinition.", this);
                return;
            }

            SpawnPiece(pieceDefinition, owner, spawnCoord);
        }

        private void SpawnPiece(PieceDefinition definition, PlayerId pieceOwner, GridCoord at)
        {
            var id = new PieceId(_nextPieceId++);
            var piece = new Piece(definition, id, pieceOwner, at);
            _grid.SetOccupant(at, piece);

            var go = SceneObjectFactory.CreatePrimitive(
                PrimitiveType.Capsule, $"Piece_{definition.DisplayName}_{id.Value}", visualizer.transform, this);
            go.transform.position = visualizer.TopOf(at);
            go.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);

            go.AddComponent<PieceView>().Bind(id, _bus, visualizer.TopOf);
        }

        private void Update()
        {
            if (_processor == null || Mouse.current == null) return;
            if (!Mouse.current.leftButton.wasPressedThisFrame) return;
            if (TryGetClickedCoord(out var coord)) HandleClick(coord);
        }

        private bool TryGetClickedCoord(out GridCoord coord)
        {
            coord = default;
            if (clickCamera == null) return false;

            var ray = clickCamera.ScreenPointToRay(Mouse.current.position.ReadValue());
            return Physics.Raycast(ray, out var hit) && visualizer.TryGetCoord(hit.collider.gameObject, out coord);
        }

        private void HandleClick(GridCoord coord)
        {
            if (_selected == null)
            {
                TrySelect(coord);
                return;
            }

            // Com uma peça selecionada: clicar num destino jogável move; qualquer outro clique cancela.
            if (_selectedDestinations != null && _selectedDestinations.Contains(coord))
                _processor.Submit(_moveAction.BuildCommand(_selected, coord));

            ClearSelection();
        }

        private void TrySelect(GridCoord coord)
        {
            var piece = _grid.GetTile(coord)?.Occupant;
            if (piece == null || piece.Owner != owner) return;

            _selected = piece;
            _selectedDestinations = MoveResolver.GetPlayableDestinations(piece, _grid);
            _bus.Raise(new ReachableTilesComputedEvent(owner, _selectedDestinations));
        }

        private void ClearSelection()
        {
            if (_selected == null) return;
            _selected = null;
            _selectedDestinations = null;
            _bus.Raise(new ReachableTilesClearedEvent(owner));
        }

        private void OnDestroy() => _bus?.Dispose();
    }
}
