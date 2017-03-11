// Guids.cs
// MUST match guids.h
using System;

namespace OlegShilo.MoveTypeToFile
{
    static class GuidList
    {
        public const string guidMoveTypeToFilePkgString = "8015c581-31ee-4b02-802a-37dc73a3c7df";
        public const string guidMoveTypeToFileCmdSetString = "e417adb1-2c8e-4c4d-ad94-3a4d137a2b31";
        public const string guidMoveTypeToFileCmdSelectSetString = "e417adb1-2c8e-4c4d-ad94-3a4d126a2b31";
        public const string guidToolWindowPersistanceString = "2cfee6e1-8c7f-49b6-ae68-dcc0bf637cf4";
        public const string guidMoveTypeToFileConfigCmdSetString = "c3099c8a-d492-4fd2-8b5a-dd89ba8804e8";

        public static readonly Guid guidMoveTypeToFileCmdSet = new Guid(guidMoveTypeToFileCmdSetString);
        public static readonly Guid guidMoveTypeToFileSelectCmdSet = new Guid(guidMoveTypeToFileCmdSelectSetString);
        public static readonly Guid guidMoveTypeToFileConfigCmdSet = new Guid(guidMoveTypeToFileConfigCmdSetString);
    };
}