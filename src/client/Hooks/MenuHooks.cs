﻿using Menu;

namespace Client.Hooks;

sealed class MenuHooks
{
    public void Hook()
    {
        On.Menu.MainMenu.AddMainMenuButton += MainMenu_AddMainMenuButton;
        On.Menu.MainMenu.Update += MainMenu_Update;
    }

    private void MainMenu_AddMainMenuButton(On.Menu.MainMenu.orig_AddMainMenuButton orig, MainMenu self, SimpleButton button, Action callback, int indexFromBottomOfList)
    {
        orig(self, button, callback, indexFromBottomOfList);

        // Add MULTI PLAYER button at location of REGIONS button
        if (button.signalText == "REGIONS") {
            self.AddMainMenuButton(
                new SimpleButton(self, self.pages[0], "WORLDWIDE", "RWW", button.pos, button.size), WorldwidePressed, 0
            );
        }
    }

    void WorldwidePressed()
    {
        connectingGreyed = true;
        ClientNet.State.Connect("localhost", Packets.DefaultPort);
    }

    bool warningAdded;
    bool connectingGreyed;

    void MainMenu_Update(On.Menu.MainMenu.orig_Update orig, MainMenu self)
    {
        orig(self);

        // Grey everything out while connecting to self
        if (connectingGreyed) {
            foreach (SimpleButton simpleButton in self.mainMenuButtons) {
                simpleButton.buttonBehav.greyedOut = true;
            }
        }

        if (ClientNet.State.Progress == ConnectionProgress.Connecting) {
            connectingGreyed = true;
        } else if (ClientNet.State.Progress == ConnectionProgress.Disconnected) {
            connectingGreyed = false;
        } else if (ClientNet.State.Progress == ConnectionProgress.Connected && IntroduceClient.Queue.Latest(out var p)) {
            Log($"Joining game: {p}");
            ClientNet.State.IntroducedToSession(p);
            self.manager.RequestMainProcessSwitch(ProcessManager.ProcessID.Game);
        }

        // Prevent accidentally enabling MSC
        if (ModManager.MSC) {
            if (!warningAdded) {
                warningAdded = true;
                MenuLabel label = new(self, self.pages[0], "DISABLE MORE SLUGCATS EXPANSION", new(683, 50), Vector2.zero, true);
                label.label.color = Color.red;
                self.pages[0].subObjects.Add(label);
            }

            foreach (SimpleButton simpleButton in self.mainMenuButtons) {
                simpleButton.buttonBehav.greyedOut = simpleButton.signalText != "REMIX";
            }
        }
    }
}