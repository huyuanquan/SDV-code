using System;
using System.IO;

namespace Netcode;

public class NetRotation : NetField<float, NetRotation>
{
	public NetRotation()
	{
	}

	public NetRotation(float value)
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
		float num = Math.Abs(endValue - startValue);
		float period = (float)Math.PI * 2f;
		if (num > 180f)
		{
			if (endValue > startValue)
			{
				startValue += period;
			}
			else
			{
				endValue += period;
			}
		}
		return (startValue + (endValue - startValue) * factor) % period;
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
