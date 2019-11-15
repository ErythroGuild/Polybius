using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Polybius {
	class Entry {
		public Type type;
		public string name;
		public string URL;

		public enum Type {
			Unknown = -1,
			Spell = 0,
			Talent,
			Trait,
			// Essence,	// Not a separate type of spell
			Mount,
			Pet,
			PetSpell,
			Item,
			Currency,
			Title,
			Achieve,
			Quest,
			Faction
		};
	}
}
