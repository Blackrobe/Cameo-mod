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
	[Desc("Produces larva actors periodically for Zerg-style unit production.")]
	public class LarvaSpawnerInfo : PausableConditionalTraitInfo
	{
		[ActorReference]
		[FieldLoader.Require]
		[Desc("The larva actor to produce.")]
		public readonly string LarvaActor = null;

		[Desc("Production queue type to use for spawning larvae.")]
		public readonly string Type = "Larva";

		[Desc("Duration between larva spawns.")]
		public readonly int SpawnInterval = 1000;

		[Desc("Maximum number of larvae that can exist at once.")]
		public readonly int MaxLarvae = 3;

		[Desc("Immediately spawn initial larvae.")]
		public readonly bool Immediate = false;

		[Desc("Show a selection bar for larva production progress.")]
		public readonly bool ShowSelectionBar = true;

		public readonly Color ChargeColor = Color.DarkOrange;

		[Desc("Defines to which players the bar is to be shown.")]
		public readonly PlayerRelationship SelectionBarDisplayRelationships = PlayerRelationship.Ally;

		public override object Create(ActorInitializer init) { return new LarvaSpawner(init, this); }
	}

	public class LarvaSpawner : PausableConditionalTrait<LarvaSpawnerInfo>, ISelectionBar, ITick, ISync, INotifyOwnerChanged
	{
		readonly LarvaSpawnerInfo info;
		Actor self;

		[Sync]
		int ticks;

		public LarvaSpawner(ActorInitializer init, LarvaSpawnerInfo info)
			: base(info)
		{
			this.info = info;
			self = init.Self;
			ticks = info.Immediate ? 0 : info.SpawnInterval;
		}

		void ITick.Tick(Actor self)
		{
			if (IsTraitDisabled || IsTraitPaused)
				return;

			// Count existing larvae
			var existingLarvae = self.World.ActorsHavingTrait<Larva>()
				.Count(a => a.Owner == self.Owner && !a.IsDead);

			if (existingLarvae >= info.MaxLarvae)
				return;

			if (--ticks < 0)
			{
				var production = self.TraitsImplementing<Production>()
					.FirstOrDefault(p => !p.IsTraitDisabled && !p.IsTraitPaused && p.Info.Produces.Contains(info.Type));

				if (production != null)
				{
					var larvaActorInfo = self.World.Map.Rules.Actors[info.LarvaActor.ToLowerInvariant()];
					var inits = new TypeDictionary
					{
						new OwnerInit(self.Owner),
						new FactionInit(production.Faction)
					};

					if (production.Produce(self, larvaActorInfo, info.Type, inits, 0))
					{
						// Successfully spawned larva
					}
				}

				ticks = info.SpawnInterval;
			}
		}

		protected override void TraitEnabled(Actor self)
		{
			ticks = info.SpawnInterval;
		}

		void INotifyOwnerChanged.OnOwnerChanged(Actor self, OpenRA.Player oldOwner, OpenRA.Player newOwner)
		{
			ticks = info.Immediate ? 0 : info.SpawnInterval;
		}

		float ISelectionBar.GetValue()
		{
			if (!info.ShowSelectionBar || IsTraitDisabled)
				return 0f;

			var viewer = self.World.RenderPlayer ?? self.World.LocalPlayer;
			if (viewer != null && !Info.SelectionBarDisplayRelationships.HasRelationship(self.Owner.RelationshipWith(viewer)))
				return 0f;

			return (float)(info.SpawnInterval - ticks) / info.SpawnInterval;
		}

		Color ISelectionBar.GetColor()
		{
			return info.ChargeColor;
		}

		bool ISelectionBar.DisplayWhenEmpty
		{
			get
			{
				if (!info.ShowSelectionBar || IsTraitDisabled)
					return false;

				var viewer = self.World.RenderPlayer ?? self.World.LocalPlayer;
				if (viewer != null && !Info.SelectionBarDisplayRelationships.HasRelationship(self.Owner.RelationshipWith(viewer)))
					return false;

				return true;
			}
		}
	}
}
