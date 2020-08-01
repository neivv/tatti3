/// A single dat requirement in AppState.

using System;
using System.Collections.Generic;

namespace Tatti3.GameData
{
    public struct Requirement
    {
        public UInt16 Opcode { get; set; }
        public UInt16 Param { get; set; }

        public static List<Requirement> ListFromRaw(UInt16[] raw)
        {
            // Skip past entry id
            var i = 1;
            var result = new List<Requirement>();
            while (i < raw.Length)
            {
                UInt16 param = 0;
                UInt16 opcode = raw[i];
                switch (opcode)
                {
                    case 0xff02: case 0xff03: case 0xff04: case 0xff25:
                        param = raw[i + 1];
                        i += 1;
                        break;
                    default:
                        break;
                }
                i += 1;
                result.Add(new Requirement {
                    Opcode = opcode,
                    Param = param,
                });
            }
            return result;
        }
    }
}
