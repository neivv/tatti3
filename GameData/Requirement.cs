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
            var i = 0;
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

        // Returns without last 0xffff
        public static UInt16[] ToRaw(Requirement[] reqs)
        {
            var result = new List<UInt16>();
            foreach (var req in reqs)
            {
                result.Add(req.Opcode);
                switch (req.Opcode)
                {
                    case 0xff02: case 0xff03: case 0xff04: case 0xff25:
                        result.Add(req.Param);
                        break;
                    default:
                        break;
                }
            }
            return result.ToArray();
        }

        public bool IsUpgradeLevelOpcode()
        {
            return Opcode >= 0xff1f && Opcode <= 0xff21;
        }

        public bool IsEnd()
        {
            return Opcode == 0xffff;
        }

        public override bool Equals(object? obj)
        {
            return obj is Requirement value &&
                Opcode == value.Opcode &&
                Param == value.Param;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Opcode, Param);
        }

        public static bool operator ==(Requirement left, Requirement right)
        {
            return EqualityComparer<Requirement>.Default.Equals(left, right);
        }

        public static bool operator !=(Requirement left, Requirement right)
        {
            return !(left == right);
        }
    }
}
