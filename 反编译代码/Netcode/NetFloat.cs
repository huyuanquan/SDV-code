using System.IO;

namespace Netcode;

public class NetFloat : NetField<float, NetFloat>
{
	public NetFloat()
	{
	}

	public NetFloat(float value)
		: base(value)
	{
	}

	public override void Set(float newValue)
	{
		if (base.canShortcutSet())
		{
			base.value = newValue;
		}
		else if (newValue != base.value)
		{
			base.cleanSet(newValue);
			base.MarkDirty();
		}
	}

	protected override float interpolate(float startValue, float endValue, float factor)
	{
		return startValue + (endValue - startValue) * factor;
	}

	protected override void ReadDelta(BinaryReader reader, NetVersion version)
	{
		float newValue = reader.ReadSingle();
		if (version.IsPriorityOver(base.ChangeVersion))
		{
			base.setInterpolationTarget(newValue);
		}
	}

	protected override void WriteDelta(BinaryWriter writer)
	{
		writer.Write(base.value);
	}
}
