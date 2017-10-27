﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LiveRoku.Core {
    internal class RoomInfo : Base.IRoomInfo {
        public Base.LiveStatus LiveStatus { get; internal set; }
        public bool IsOn { get; internal set; }
        public string Title { get; internal set; }
        public int TimeLine { get; internal set; }
        public string Anchor { get; internal set; }
        public string RawData { get; internal set; }
        
        public override string ToString () {
            return $"IsOn : {IsOn}, LiveStatus : {LiveStatus}, Title : {Title}";
        }
    }
}