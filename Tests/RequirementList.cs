using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tatti3;
using Tatti3.GameData;

namespace Tests
{
    [TestClass]
    public class RequirementListTests
    {
        static Requirement Req(UInt16 id)
        {
            return new Requirement
            {
                Opcode = id,
                Param = 0x0000,
            };
        }

        void VerifyList(RequirementList list, string ctx)
        {
            bool inUpgradeJump = false;
            bool executedEnd = false;
            for (int i = 0; i < list.Count; i++)
            {
                var msg = $"{list.ToString()} is broken @ {i}, ({ctx})";
                var req = list[i].Value;
                if (req.IsEnd())
                {
                    Assert.IsTrue(inUpgradeJump, msg);
                    Assert.IsFalse(executedEnd, msg);
                    inUpgradeJump = false;
                    executedEnd = true;
                }
                else if (req.IsUpgradeLevelOpcode())
                {
                    Assert.IsFalse(inUpgradeJump, msg);
                    executedEnd = false;
                    inUpgradeJump = true;
                }
                else
                {
                    executedEnd = false;
                }
            }
            Assert.IsFalse(executedEnd, $"{list.ToString()} ended with a unnecessary end ({ctx})");
        }

        void MutateAtEveryPosition(RequirementList list)
        {
            VerifyList(list, "");
            for (int i = 0; i < list.Count; i++)
            {
                var copy1 = new RequirementList(list);
                if (!copy1[i].Value.IsEnd())
                {
                    copy1[i] = new RequirementList.RequirementWrap(Req(0xff10));
                }
                VerifyList(copy1, $"Index {i} to non upgrade");
                var copy2 = new RequirementList(list);
                if (!copy2[i].Value.IsEnd())
                {
                    copy2[i] = new RequirementList.RequirementWrap(Req(0xff20));
                }
                VerifyList(copy2, $"Index {i} to upgrade");
            }
        }

        [TestMethod]
        public void UpgradeLevelJumps()
        {
            var list = new RequirementList();
            list.Rebuild(Add => {
                Add(Req(0xff15));
                Add(Req(0x0022));
                Add(Req(0xff1f));
                Add(Req(0xff11));
                Add(Req(0xffff));
                Add(Req(0xff20));
                Add(Req(0xff02));
                Add(Req(0xff01));
                Add(Req(0xffff));
                Add(Req(0xff21));
                Add(Req(0xff02));
            });
            MutateAtEveryPosition(list);
        }

        [TestMethod]
        public void UpgradeLevelJumps2()
        {
            var list = new RequirementList();
            list.Rebuild(Add => {
                Add(Req(0xff15));
                Add(Req(0x0022));
                Add(Req(0xff1f));
                Add(Req(0xffff));
                Add(Req(0xff20));
                Add(Req(0xff02));
                Add(Req(0xff01));
                Add(Req(0xffff));
                Add(Req(0xff21));
            });
            MutateAtEveryPosition(list);
        }

        [TestMethod]
        public void UpgradeLevelJumps3()
        {
            var list = new RequirementList();
            list.Rebuild(Add => {
                Add(Req(0xff15));
                Add(Req(0x0022));
                Add(Req(0xff1f));
                Add(Req(0xffff));
                Add(Req(0xff20));
                Add(Req(0xff02));
                Add(Req(0xff01));
                Add(Req(0xffff));
                Add(Req(0xff21));
            });
            MutateAtEveryPosition(list);
        }
    }
}
