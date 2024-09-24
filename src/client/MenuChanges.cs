using Menu;

namespace Client;

sealed internal class MenuChanges
{
    static readonly string signal = "RAIN_WORLDWIDE_ONLINE";
    
    public void Hook()
    {
        On.Menu.MainMenu.AddMainMenuButton += MainMenu_AddMainMenuButton; ;
        On.Menu.MainMenu.Update += MainMenu_Update;
    }

    private void MainMenu_AddMainMenuButton(On.Menu.MainMenu.orig_AddMainMenuButton orig, MainMenu self, SimpleButton button, Action callback, int indexFromBottomOfList)
    {
        orig(self, button, callback, indexFromBottomOfList);

        // Add MULTI PLAYER button at location of REGIONS button
        if (button.signalText == "REGIONS") {
            self.AddMainMenuButton(
                new SimpleButton(self, self.pages[0], "WORLDWIDE", signal, button.pos, button.size),
                () => WorldwidePressed(self),
                0
            );
        }
    }

    void WorldwidePressed(MainMenu menu) 
    {
        ClientNetState.Initialize();
    }

    bool warningAdded;

    void MainMenu_Update(On.Menu.MainMenu.orig_Update orig, MainMenu self)
    {
        orig(self);

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
