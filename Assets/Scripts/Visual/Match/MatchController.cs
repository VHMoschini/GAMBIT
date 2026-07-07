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
    /// Bootstrap do vertical slice: monta o pipeline (bus + registro + processor + contexto), posiciona
    /// duas peças (uma por lado) e traduz cliques em movimento/ataque. Demonstra a stack inteira de ponta
    /// a ponta — input → comando → core → evento → visual — incluindo a cadeia derivada ataque → dano → morte.
    ///
    /// <para>Ainda é o ancestral mínimo do <c>HumanController</c> (Marco 4): sem wizard de seleção em pilha,
    /// turno, stamina ou gate de input durante a animação. Aqui o "controlador" só existe para o Player1;
    /// o Player2 é um alvo estático até o Marco 4 trazer dois controllers alternando.</para>
    /// </summary>
    public sealed class MatchController : MonoBehaviour
    {
        [Header("Cena")]
        [SerializeField] private GridRuntime gridRuntime;
        [SerializeField] private GridVisualizer visualizer;
        [SerializeField] private TileHighlighter highlighter;
        [SerializeField] private Camera clickCamera;

        [Header("Peça do jogador (Player1)")]
        [SerializeField] private PieceDefinition pieceDefinition;
        [SerializeField] private GridCoord spawnCoord = new GridCoord(0, 0);

        [Header("Peça inimiga (Player2)")]
        [SerializeField] private PieceDefinition enemyDefinition;
        [SerializeField] private GridCoord enemySpawnCoord = new GridCoord(0, 2);

        private const PlayerId Owner = PlayerId.Player1;

        private GameEventBus _bus;
        private CommandProcessor _processor;
        private GameContext _context;
        private GridSystem _grid;
        private MoveActionDefinition _moveAction;
        private AttackActionDefinition _attackAction;

        private Piece _selected;
        private IReadOnlyList<GridCoord> _selectedDestinations;
        private IReadOnlyList<GridCoord> _attackTargets;
        private int _nextPieceId = 1; // 0 fica reservado como "sem alvo" (ver AttackActionDefinition).

        private void Start()
        {
            if (clickCamera == null) clickCamera = Camera.main;

            visualizer.EnsureBuilt();
            gridRuntime.Build();
            _grid = gridRuntime.Grid;

            _bus = new GameEventBus();
            _context = new GameContext(_grid, _bus, new PieceRegistry());
            _processor = new CommandProcessor(_context);
            if (highlighter != null) highlighter.Bind(_bus);

            _moveAction = pieceDefinition.Actions.OfType<MoveActionDefinition>().FirstOrDefault();
            _attackAction = pieceDefinition.Actions.OfType<AttackActionDefinition>().FirstOrDefault();
            if (_moveAction == null)
            {
                Debug.LogError($"{nameof(MatchController)}: a PieceDefinition '{pieceDefinition?.DisplayName}' não tem MoveActionDefinition.", this);
                return;
            }

            SpawnPiece(pieceDefinition, Owner, spawnCoord);
            if (enemyDefinition != null) SpawnPiece(enemyDefinition, PlayerId.Player2, enemySpawnCoord);
        }

        private void SpawnPiece(PieceDefinition definition, PlayerId pieceOwner, GridCoord at)
        {
            var id = new PieceId(_nextPieceId++);
            var piece = new Piece(definition, id, pieceOwner, at);
            _grid.SetOccupant(at, piece);
            _processor.Register(piece);

            var go = SceneObjectFactory.CreatePrimitive(
                PrimitiveType.Capsule, $"Piece_{definition.DisplayName}_{id.Value}", visualizer.transform, this);
            go.transform.position = visualizer.TopOf(at);
            go.transform.localScale = new Vector3(0.6f, 0.6f, 0.6f);

            // Sem collider na peça: o raycast de clique resolve sempre o tile embaixo (seleção/alvo por casa).
            var collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

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

            // Com uma peça selecionada: destino jogável move; inimigo no alcance ataca; o resto cancela.
            if (_selectedDestinations != null && _selectedDestinations.Contains(coord))
                _processor.Submit(_moveAction.BuildCommand(_selected, coord, _context));
            else if (_attackTargets != null && _attackTargets.Contains(coord))
                _processor.Submit(_attackAction.BuildCommand(_selected, coord, _context));

            ClearSelection();
        }

        private void TrySelect(GridCoord coord)
        {
            var piece = _grid.GetTile(coord)?.Occupant;
            if (piece == null || piece.Owner != Owner) return;

            _selected = piece;
            _selectedDestinations = MoveResolver.GetPlayableDestinations(piece, _grid);
            _attackTargets = ComputeAttackTargets(piece);
            _bus.Raise(new ReachableTilesComputedEvent(Owner, _selectedDestinations));
        }

        /// <summary>Casas de inimigos vivos dentro do alcance (Chebyshev) da ação de ataque da peça.</summary>
        private IReadOnlyList<GridCoord> ComputeAttackTargets(Piece attacker)
        {
            var targets = new List<GridCoord>();
            if (_attackAction == null) return targets;

            foreach (var tile in _grid.AllTiles)
            {
                var occupant = tile.Occupant;
                if (occupant == null || occupant.Owner == attacker.Owner) continue;
                if (attacker.Position.ChebyshevDistanceTo(tile.Coord) <= _attackAction.Range)
                    targets.Add(tile.Coord);
            }

            return targets;
        }

        private void ClearSelection()
        {
            if (_selected == null) return;
            _selected = null;
            _selectedDestinations = null;
            _attackTargets = null;
            _bus.Raise(new ReachableTilesClearedEvent(Owner));
        }

        private void OnDestroy() => _bus?.Dispose();
    }
}
