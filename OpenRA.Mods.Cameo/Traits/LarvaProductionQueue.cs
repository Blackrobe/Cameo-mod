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
using OpenRA.Mods.Common;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Cameo.Traits
{
	[Desc("A production queue that consumes the larva actor when producing units.",
		"The new unit is spawned at the larva's current position, bypassing exit-cell checks.")]
	public class LarvaProductionQueueInfo : ProductionQueueInfo
	{
		public override object Create(ActorInitializer init) { return new LarvaProductionQueue(init, this); }
	}

	public class LarvaProductionQueue : ProductionQueue
	{
		public LarvaProductionQueue(ActorInitializer init, ProductionQueueInfo info)
			: base(init, info)
		{
		}

		protected override bool BuildUnit(ActorInfo unit)
		{
			if (!Actor.IsInWorld || Actor.IsDead)
			{
				CancelProduction(unit.Name, 1);
				return false;
			}

			var item = Queue.FirstOrDefault(i => i.Done && i.Item == unit.Name);
			if (item == null)
				return false;

			// Spawn the new unit at the larva's current world position, bypassing
			// the exit-cell system so map edges / adjacent buildings don't block production.
			var inits = new TypeDictionary
			{
				new OwnerInit(Actor.Owner),
				new FactionInit(BuildableInfo.GetInitialFaction(unit, Faction))
			};

			var newUnit = Actor.World.CreateActor(false, unit.Name, inits);
			var positionable = newUnit.TraitOrDefault<IPositionable>();
			positionable?.SetPosition(newUnit, Actor.CenterPosition);
			Actor.World.Add(newUnit);

			EndProduction(item);

			// Remove the larva in the next frame after the new unit is live in the world.
			Actor.World.AddFrameEndTask(w =>
			{
				if (!Actor.IsDead)
					Actor.Kill(Actor);
			});

			return true;
		}
	}
}
