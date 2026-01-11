#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System.Linq;
using OpenRA.Primitives;
using OpenRA.Traits;
using OpenRA.Mods.Common.Traits;
using System;

namespace OpenRA.Mods.Cameo.Traits
{
	[Desc("Applies linear changes to PhysicalState. Will be scaled if the PhysicalState has RelativeToHealth enabled.")]
	public class ChangesPhysicalStateInfo : ConditionalTraitInfo, Requires<PhysicalStateInfo>
	{
		[FieldLoader.Require]
		[Desc("Name of the PhysicalState to change.")]
		public readonly string PhysicalStateName = null;

		[Desc("Change value.")]
		public readonly int Amount = 10;

		[Desc("Ticks between change applications.")]
		public readonly int Delay = 25;

		[Desc("Cap off change if it passes RelaxedValue.")]
		public readonly bool IsRelaxation = false;

		public override object Create(ActorInitializer init) { return new ChangesPhysicalState(init.Self, this); }
	}

	public class ChangesPhysicalState : ConditionalTrait<ChangesPhysicalStateInfo>, ITick, ISync
	{
		readonly Actor self;
		readonly PhysicalState physicalState;
		int relaxedValue;

		[Sync]
		int ticks;

		public ChangesPhysicalState(Actor self, ChangesPhysicalStateInfo info)
			: base(info)
		{
			this.self = self;
			physicalState = self.TraitsImplementing<PhysicalState>()
				.FirstOrDefault(ps => ps.Name == info.PhysicalStateName);
			relaxedValue = physicalState.Info.RelaxedValue;
		}

		void ApplyChange()
		{
			var amount = Info.Amount;
			if (Info.IsRelaxation)
			{
				var difference = relaxedValue - physicalState.Value;
				amount = difference > 0
					? Math.Min(amount, difference)
					: Math.Max(amount, difference);
			}
			physicalState?.ApplyChange(amount, self, Info.IsRelaxation);
		}

		void ITick.Tick(Actor self)
		{
			if (IsTraitDisabled || physicalState == null)
				return;

			if (++ticks >= Info.Delay)
			{
				ticks = 0;
				ApplyChange();
			}
		}
	}
}
