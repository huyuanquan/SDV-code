using Microsoft.Xna.Framework;

namespace StardewValley.Menus;

public class ServerConnectionDialog : ConfirmationDialog
{
	public ServerConnectionDialog(behavior onConfirm = null, behavior onCancel = null)
		: base(Game1.content.LoadString("Strings\\UI:CoopMenu_Connecting"), onConfirm, onCancel)
	{
		base.okButton.visible = false;
		if (Game1.options.SnappyMenus)
		{
			this.populateClickableComponentList();
			this.snapToDefaultClickableComponent();
		}
	}

	public override void update(GameTime time)
	{
		base.update(time);
		if (Game1.server != null && Game1.server.connected())
		{
			base.confirm();
		}
	}
}
