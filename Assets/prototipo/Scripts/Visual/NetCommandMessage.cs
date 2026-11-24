using System;
using Prototipo.Core;

namespace Prototipo.Visual
{
    /// <summary>
    /// Mensagem serializavel (JSON compacto via JsonUtility) enviada pela sala Steam
    /// para sincronizar as acoes deterministicas do <see cref="MatchEngine"/> entre Host (P1) e Cliente (P2).
    /// </summary>
    [Serializable]
    public sealed class NetCommandMessage
    {
        public const string TypeClientReady = "client_ready";
        public const string TypeSyncStart = "sync_start";
        public const string TypePlace = "place";
        public const string TypeRemove = "remove";
        public const string TypeAutoFill = "autofill";
        public const string TypeConfirm = "confirm";
        public const string TypeUnlock = "unlock";
        public const string TypeMove = "move";
        public const string TypeAttack = "attack";
        public const string TypeEndTurn = "endturn";
        public const string TypeRestart = "restart";

        public string type;
        public int team;
        public string specId;
        public int pieceId;
        public int x;
        public int z;
        public int seq;

        public PlayerTeam TeamEnum => team == (int)PlayerTeam.Player2 ? PlayerTeam.Player2 : PlayerTeam.Player1;
        public BoardCoord Coord => new BoardCoord(x, z);

        public static NetCommandMessage ClientReady()
        {
            return new NetCommandMessage { type = TypeClientReady };
        }

        public static NetCommandMessage SyncStart()
        {
            return new NetCommandMessage { type = TypeSyncStart };
        }

        public static NetCommandMessage Place(PlayerTeam team, string specId, BoardCoord coord)
        {
            return new NetCommandMessage
            {
                type = TypePlace,
                team = (int)team,
                specId = specId ?? string.Empty,
                x = coord.X,
                z = coord.Z
            };
        }

        public static NetCommandMessage Remove(PlayerTeam team, BoardCoord coord)
        {
            return new NetCommandMessage
            {
                type = TypeRemove,
                team = (int)team,
                x = coord.X,
                z = coord.Z
            };
        }

        public static NetCommandMessage AutoFill(PlayerTeam team)
        {
            return new NetCommandMessage
            {
                type = TypeAutoFill,
                team = (int)team
            };
        }

        public static NetCommandMessage Confirm(PlayerTeam team)
        {
            return new NetCommandMessage
            {
                type = TypeConfirm,
                team = (int)team
            };
        }

        public static NetCommandMessage Unlock(PlayerTeam team, int pieceId)
        {
            return new NetCommandMessage
            {
                type = TypeUnlock,
                team = (int)team,
                pieceId = pieceId
            };
        }

        public static NetCommandMessage Move(PlayerTeam team, int pieceId, BoardCoord destination)
        {
            return new NetCommandMessage
            {
                type = TypeMove,
                team = (int)team,
                pieceId = pieceId,
                x = destination.X,
                z = destination.Z
            };
        }

        public static NetCommandMessage Attack(PlayerTeam team, int attackerId, BoardCoord targetCoord)
        {
            return new NetCommandMessage
            {
                type = TypeAttack,
                team = (int)team,
                pieceId = attackerId,
                x = targetCoord.X,
                z = targetCoord.Z
            };
        }

        public static NetCommandMessage EndTurn(PlayerTeam team)
        {
            return new NetCommandMessage
            {
                type = TypeEndTurn,
                team = (int)team
            };
        }

        public static NetCommandMessage Restart()
        {
            return new NetCommandMessage { type = TypeRestart };
        }
    }
}
