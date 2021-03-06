﻿namespace LiveRoku.Core.Common {

    public interface ITransform {
        bool isActive ();
        System.Threading.Tasks.Task connectAsync (string host, int port);
        bool writeAndFlush (byte[] data);
        bool write (byte[] data);
        bool flush ();
        void close ();
    }

    public interface ITransformContext : ITransform {
        void fireConnected ();
        void fireRead (object data);
        void fireReadReady (object data);
        void fireClosed (object data);
        void fireException (System.Exception e);
    }

}