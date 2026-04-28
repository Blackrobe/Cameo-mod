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

using OpenRA.Traits;

namespace OpenRA.Mods.Cameo.Traits
{
	[Desc("Marks an actor as a larva that can be used for Zerg-style unit production.")]
	public class LarvaInfo : TraitInfo
	{
		public override object Create(ActorInitializer init) { return new Larva(init, this); }
	}

	public class Larva : INotifyCreated
	{
		public readonly LarvaInfo Info;

		public Larva(ActorInitializer init, LarvaInfo info)
		{
			Info = info;
		}

		void INotifyCreated.Created(Actor self)
		{
			// Larva is created and ready for use
		}
	}
}
