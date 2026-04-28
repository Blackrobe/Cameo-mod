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

		void SpawnAtLarva(ActorInfo actorInfo, TypeDictionary inits)
		{
			var newUnit = Actor.World.CreateActor(false, actorInfo.Name, inits);
			var positionable = newUnit.TraitOrDefault<IPositionable>();
			positionable?.SetPosition(newUnit, Actor.CenterPosition);
			Actor.World.Add(newUnit);
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

			var bi = BuildableInfo.GetTraitForQueue(unit, Info.Type);
			var inits = new TypeDictionary
			{
				new OwnerInit(Actor.Owner),
				new FactionInit(BuildableInfo.GetInitialFaction(unit, Faction))
			};

			// Mirror Production.ProduceActors: respect BuildAmount and AdditionalActors,
			// but spawn everything at the larva's world position instead of an exit cell.
			var buildAmount = bi?.BuildAmount ?? 1;
			var additionalActors = bi?.AdditionalActors ?? [];
			for (var n = 0; n < buildAmount; n++)
			{
				SpawnAtLarva(unit, inits);
				foreach (var additionalActor in additionalActors)
				{
					var additionalInfo = Actor.World.Map.Rules.Actors[additionalActor.ToLowerInvariant()];
					SpawnAtLarva(additionalInfo, inits);
				}
			}

			EndProduction(item);

			// Remove the larva in the next frame after the new units are live in the world.
			Actor.World.AddFrameEndTask(w =>
			{
				if (!Actor.IsDead)
					Actor.Kill(Actor);
			});

			return true;
		}
	}
}
