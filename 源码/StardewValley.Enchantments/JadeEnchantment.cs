using StardewValley.Tools;

namespace StardewValley.Enchantments;

public class JadeEnchantment : BaseWeaponEnchantment
{
	protected override void _ApplyTo(Item item)
	{
		base._ApplyTo(item);
		if (item is MeleeWeapon weapon)
		{
			weapon.critMultiplier.Value += 0.1f * (float)base.GetLevel();
		}
	}

	protected override void _UnapplyTo(Item item)
	{
		base._UnapplyTo(item);
		if (item is MeleeWeapon weapon)
		{
			weapon.critMultiplier.Value -= 0.1f * (float)base.GetLevel();
		}
	}

	public override bool ShouldBeDisplayed()
	{
		return false;
	}

	public override bool IsForge()
	{
		return true;
	}
}
