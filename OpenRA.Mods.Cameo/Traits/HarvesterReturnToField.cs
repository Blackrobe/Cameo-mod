#region Copyright & License Information
/**
 * Copyright (c) The Cameo Developers (see CREDITS).
 * This file is part of Cameo, which is free software.
 * It is made available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of the License,
 * or (at your option) any later version. For more information, see COPYING.
 */
#endregion

using OpenRA.Mods.Common.Activities;
using OpenRA.Mods.Common.Traits;
using OpenRA.Traits;

namespace OpenRA.Mods.Cameo.Traits
{
	[Desc("Makes harvesters remember their last harvested field and return to it after unloading at a refinery,",
		"instead of defaulting to the nearest resources from the refinery.")]
	public class HarvesterReturnToFieldInfo : TraitInfo
	{
		public override object Create(ActorInitializer init) => new HarvesterReturnToField(this);
	}

	public class HarvesterReturnToField : INotifyHarvestAction, INotifyDockClient, IResolveOrder
	{
		CPos? lastHarvestedCell;
		bool skipNextReturn;

		public HarvesterReturnToField(HarvesterReturnToFieldInfo info) { }

		// Record the cell each time the harvester begins moving to a specific resource cell.
		void INotifyHarvestAction.MovingToResources(Actor self, CPos targetCell)
		{
			lastHarvestedCell = targetCell;
		}

		void INotifyHarvestAction.MovementCancelled(Actor self) { }
		void INotifyHarvestAction.Harvested(Actor self, string resourceType) { }
		void INotifyDockClient.Docked(Actor self, Actor host) { }

		// When the player manually orders a dock, remember to skip the return-to-field
		// override so the harvester searches for the nearest resources instead.
		void IResolveOrder.ResolveOrder(Actor self, Order order)
		{
			if (order.OrderString == "Dock" || order.OrderString == "ForceDock")
				skipNextReturn = true;
		}

		// Called by GenericDockSequence immediately after Harvester.OnDockCompleted queues
		// a new FindAndDeliverResources(null) — which would cause the harvester to search
		// from the refinery. We cancel that activity and replace it with one that resumes
		// at the last remembered field instead.
		void INotifyDockClient.Undocked(Actor self, Actor host)
		{
			if (skipNextReturn)
			{
				skipNextReturn = false;
				return;
			}

			if (!lastHarvestedCell.HasValue)
				return;

			// Walk to the last activity in the root queue (the FAD(null) just appended by the engine).
			var activity = self.CurrentActivity;
			if (activity == null)
				return;

			while (activity.NextActivity != null)
				activity = activity.NextActivity;

			if (activity is FindAndDeliverResources)
			{
				activity.Cancel(self);
				self.QueueActivity(false, new FindAndDeliverResources(self, lastHarvestedCell));
			}
		}
	}
}
