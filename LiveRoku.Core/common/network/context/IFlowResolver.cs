﻿namespace LiveRoku.Core.Common {
    public class FlowArgs {
        public bool Continuable { get; set; } = true;
        public object Data { get; set; }
        public static FlowArgs wrap (object data) {
            return new FlowArgs { Data = data };
        }
        public static FlowArgs newEmpty () {
            return new FlowArgs ();
        }
    }
    public interface IFlowResolver : System.IDisposable {
        void onActive (ITransformContext ctx);
        void onReadReady (ITransformContext ctx, object data);
        void onRead (ITransformContext ctx, object data);
        void onInactive (ITransformContext ctx, object data);
        void onException (ITransformContext ctx, System.Exception e);
    }

}