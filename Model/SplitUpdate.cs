using System;
using LiveSplit.Model;

namespace LiveSplit.Racetime.Model
{
    public class SplitUpdate : RTModelBase
    {
        public string SplitName
        {
            get
            {
                return Data.split_name;
            }
        }

        public TimeSpan? SplitTime
        {
            get
            {
                if (Data.split_time == "-")
                    return null;
                return TimeSpanParser.Parse(Data.split_time);
            }
        }

        public bool IsUndo
        {
            get
            {
                return Data.is_undo;
            }
        }

        public bool IsFinish
        {
            get
            {
                return Data.is_finish;
            }
        }

        public string UserID
        {
            get
            {
                return Data.user_id;
            }
        }
    }
}
