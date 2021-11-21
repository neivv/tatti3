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
            return new Requirement(id);
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

        [TestMethod]
        public void UpgradeLevelJumps4()
        {
            var list = new RequirementList();
            list.Insert(0, new RequirementList.RequirementWrap(Req(0xff11)));
            list.Insert(1, new RequirementList.RequirementWrap(Req(0xff1f)));
            list.Insert(2, new RequirementList.RequirementWrap(Req(0xff20)));
            MutateAtEveryPosition(list);
            var arr = list.ToArray();
            Assert.AreEqual(arr.Length, 4);
            Assert.AreEqual(arr[0], Req(0xff11));
            Assert.AreEqual(arr[1], Req(0xff1f));
            Assert.AreEqual(arr[2], Req(0xffff));
            Assert.AreEqual(arr[3], Req(0xff20));
        }

        [TestMethod]
        public void UpgradeLevelJumps5()
        {
            var list = new RequirementList();
            list.Insert(0, new RequirementList.RequirementWrap(Req(0xff11)));
            list.Insert(1, new RequirementList.RequirementWrap(Req(0xff11)));
            list.Insert(2, new RequirementList.RequirementWrap(Req(0xff11)));
            list.Insert(3, new RequirementList.RequirementWrap(Req(0xff11)));
            list[1] = new RequirementList.RequirementWrap(Req(0xff1f));
            list[3] = new RequirementList.RequirementWrap(Req(0xff20));
            list[5] = new RequirementList.RequirementWrap(Req(0xff21));
            MutateAtEveryPosition(list);
            var arr = list.ToArray();
            Assert.AreEqual(arr.Length, 6);
            Assert.AreEqual(arr[0], Req(0xff11));
            Assert.AreEqual(arr[1], Req(0xff1f));
            Assert.AreEqual(arr[2], Req(0xffff));
            Assert.AreEqual(arr[3], Req(0xff20));
            Assert.AreEqual(arr[4], Req(0xffff));
            Assert.AreEqual(arr[5], Req(0xff21));
        }
    }
}
