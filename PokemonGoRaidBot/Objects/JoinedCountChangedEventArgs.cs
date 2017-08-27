using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace PokemonGoRaidBot.Objects
{
    public class JoinedCountChangedEventArgs : EventArgs
    {
        public readonly JoinCountChangeType ChangeType;
        public readonly ulong UserId;
        public readonly string UserName;
        public readonly int Count;
        public readonly DateTime? ArriveTime;

        public JoinedCountChangedEventArgs(ulong userId, string username, int count, DateTime? arriveTime, JoinCountChangeType changeType)
        {
            ChangeType = changeType;
            UserId = userId;
            UserName = username;
            Count = count;
            ArriveTime = arriveTime;
        }
    }

    public enum JoinCountChangeType
    {
        Add,
        Change,
        Remove
    }
}
