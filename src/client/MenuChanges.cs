using Menu;

namespace Client;

sealed internal class MenuChanges
{
    public void Hook()
    {
        On.Menu.MainMenu.Update += MainMenu_Update;
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
