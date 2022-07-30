/// A single dat requirement in AppState.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Tatti3.GameData
{
    public struct Requirement
    {
        public UInt16 Opcode { get; set; }
        public UInt16[] Params { get; private set; }

        public Requirement(UInt16 opcode)
        {
            Opcode = opcode;
            Params = new UInt16[ParamsForOpcode(opcode)];
        }

        public static List<Requirement> ListFromRaw(UInt16[] raw)
        {
            var i = 0;
            var result = new List<Requirement>();
            while (i < raw.Length)
            {
                UInt16 opcode = raw[i];
                var opcodeCount = ParamsForOpcode(opcode);
                UInt16[] parameters = new UInt16[opcodeCount];
                for (int j = 0; j < opcodeCount; j++)
                {
                    parameters[j] = raw[i + 1 + j];
                }
                i += 1 + opcodeCount;
                result.Add(new Requirement {
                    Opcode = opcode,
                    Params = parameters,
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
                result.AddRange(req.Params);
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

        public static int ParamsForOpcode(UInt16 opcode)
        {
            return opcode switch
            {
                0xff02 or 0xff03 or 0xff04 or 0xff25 or 0xff41 => 1,
                0xff40 => 2,
                _ => 0,
            };
        }

        public override bool Equals(object? obj)
        {
            return obj is Requirement value &&
                Opcode == value.Opcode &&
                Params.SequenceEqual(value.Params);
        }

        public override int GetHashCode()
        {
            var code = new HashCode();
            code.Add(Opcode);
            foreach (var x in Params)
            {
                code.Add(x);
            }
            return code.ToHashCode();
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
