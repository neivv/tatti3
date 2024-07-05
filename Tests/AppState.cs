using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tatti3;
using Tatti3.GameData;

namespace Tests
{
    [TestClass]
    public class AppStateTests
    {
        private AppState WithDefaultData()
        {
            var fsys = new EmptyFilesystem();
            var gameData = GameData.Open(fsys);
            return new AppState(gameData);
        }

        private void Select(AppState state, ArrayFileType dat, int index)
        {
            var selections = state.Selections;
            selections[AppState.DatFileTypeToIndex(dat)] = index;
            state.Selections = selections;
        }

        [TestMethod]
        public void UnitsDatBasic()
        {
            var state = WithDefaultData();
            var gameData = state.GameData!;
            var units = state.Dat[ArrayFileType.Units];
            var hp = units.Fields[0x8];
            var supply_used = units.Fields[0x2e];
            var ground_weapon = units.Fields[0x11];
            var air_weapon = units.Fields[0x13];

            Assert.AreEqual(40u * 256u, hp.Item);
            Assert.AreEqual(2u, supply_used.Item);
            Assert.AreEqual(0u, ground_weapon.Item);
            Assert.AreEqual(0u, air_weapon.Item);

            // Zergling
            Select(state, ArrayFileType.Units, 37);

            Assert.AreEqual(35u * 256u, hp.Item);
            Assert.AreEqual(1u, supply_used.Item);
            Assert.AreEqual(35u, ground_weapon.Item);
            Assert.AreEqual(130u, air_weapon.Item);

            Assert.AreEqual(false, state.IsDirty);
            Assert.AreEqual(35u, gameData.Units.GetFieldUint(37, 0x11));
            ground_weapon.Item = 66;
            Assert.AreEqual(66u, ground_weapon.Item);
            Assert.AreEqual(true, state.IsDirty);
            Assert.AreEqual(66u, gameData.Units.GetFieldUint(37, 0x11));
            ground_weapon.Item = 35;
            Assert.AreEqual(false, state.IsDirty);
        }

        [TestMethod]
        public void SignedFields()
        {
            var state = WithDefaultData();
            var gameData = state.GameData!;
            var units = state.Dat[ArrayFileType.Units];
            var bunkerBonus = units.Fields[0x49];
            var cloakDrain = units.Fields[0x54];

            Select(state, ArrayFileType.Units, 1);

            Assert.AreEqual(64, bunkerBonus.ItemSigned);
            Assert.AreEqual(-10, cloakDrain.ItemSigned);

            bunkerBonus.ItemSigned = -5;
            cloakDrain.ItemSigned = -5;
            Assert.AreEqual(-5, bunkerBonus.ItemSigned);
            Assert.AreEqual(-5, cloakDrain.ItemSigned);

            bunkerBonus.ItemSigned = 256;
            cloakDrain.ItemSigned = 256;
            Assert.AreEqual(256, bunkerBonus.ItemSigned);
            Assert.AreEqual(256, cloakDrain.ItemSigned);

            bunkerBonus.ItemSigned = -256;
            cloakDrain.ItemSigned = -256;
            Assert.AreEqual(-256, bunkerBonus.ItemSigned);
            Assert.AreEqual(-256, cloakDrain.ItemSigned);
        }

        [TestMethod]
        public void Buttons()
        {
            var state = WithDefaultData();
            var gameData = state.GameData!;

            // Defiler
            Select(state, ArrayFileType.Units, 46);

            var units = state.Dat[ArrayFileType.Units];
            var buttons = state.Dat[ArrayFileType.Buttons];
            var unitButtons = units.Fields[0x44];
            var buttonIndex = (int)unitButtons.Item;

            Select(state, ArrayFileType.Buttons, buttonIndex);

            var buttonList = buttons.GetListFieldRef(0).Item;
            Assert.AreEqual(9, buttonList.Count);

            var VerifyButtons = (bool wasChanged) => {
                var button = buttonList[0];
                CheckButton(button, new uint[] { 1, 228, 0, 664, 41, 0, 9, 0 });

                button = buttonList[5];
                CheckButton(button, new uint[] { 7, 270, 385, 376, 23, 14, 24, 14 });

                button = buttonList[8];
                Assert.AreEqual(9u, button[0]);
                Assert.AreEqual(260u, button[1]);
                if (!wasChanged) {
                    Assert.AreEqual(0u, button[2]);
                } else {
                    Assert.AreEqual(2610u, button[2]);
                }
                Assert.AreEqual(373u, button[3]);
                Assert.AreEqual(39u, button[4]);
                Assert.AreEqual(11u, button[5]);
                Assert.AreEqual(42u, button[6]);
                Assert.AreEqual(0u, button[7]);
            };

            var button = buttonList[8];
            VerifyButtons(false);
            Assert.AreEqual(false, state.IsDirty);
            button[2] = 2610;
            VerifyButtons(true);
            Assert.AreEqual(true, state.IsDirty);
            button[2] = 0;
            VerifyButtons(false);
            Assert.AreEqual(false, state.IsDirty);
        }

        [TestMethod]
        public void AddButtons()
        {
            var state = WithDefaultData();
            var gameData = state.GameData!;

            // Defiler
            Select(state, ArrayFileType.Units, 46);

            var units = state.Dat[ArrayFileType.Units];
            var buttons = state.Dat[ArrayFileType.Buttons];
            var unitButtons = units.Fields[0x44];
            var buttonIndex = (int)unitButtons.Item;

            Select(state, ArrayFileType.Buttons, buttonIndex);

            var listRef = buttons.GetListFieldRef(0);
            var buttonList = listRef.Item;
            Assert.AreEqual(9, buttonList.Count);
            uint[] values = { 1, 2, 3, 4, 5, 6, 7, 8 };
            listRef.Insert(6, values);

            var button = buttonList[0];
            CheckButton(button, new uint[] { 1, 228, 0, 664, 41, 0, 9, 0 });
            button = buttonList[5];
            CheckButton(button, new uint[] { 7, 270, 385, 376, 23, 14, 24, 14 });
            button = buttonList[6];
            CheckButton(button, new uint[] { 1, 2, 3, 4, 5, 6, 7, 8 });
            button = buttonList[9];
            CheckButton(button, new uint[] { 9, 260, 0, 373, 39, 11, 42, 0 });

            Assert.AreEqual(true, state.IsDirty);
            listRef.Remove(6);
            Assert.AreEqual(false, state.IsDirty);

            button = buttonList[0];
            CheckButton(button, new uint[] { 1, 228, 0, 664, 41, 0, 9, 0 });
            button = buttonList[5];
            CheckButton(button, new uint[] { 7, 270, 385, 376, 23, 14, 24, 14 });
            button = buttonList[6];
            CheckButton(button, new uint[] { 8, 265, 387, 378, 23, 15, 24, 15 });
            button = buttonList[8];
            CheckButton(button, new uint[] { 9, 260, 0, 373, 39, 11, 42, 0 });
        }

        private void CheckButton(SoaStruct button, uint[] compare) {
            Assert.AreEqual(compare[0], button[0]);
            Assert.AreEqual(compare[1], button[1]);
            Assert.AreEqual(compare[2], button[2]);
            Assert.AreEqual(compare[3], button[3]);
            Assert.AreEqual(compare[4], button[4]);
            Assert.AreEqual(compare[5], button[5]);
            Assert.AreEqual(compare[6], button[6]);
            Assert.AreEqual(compare[7], button[7]);
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => button[8]);
        }
    }
}
