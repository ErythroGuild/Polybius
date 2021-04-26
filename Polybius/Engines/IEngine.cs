using System;
using System.Collections.Generic;

namespace Polybius.Engines {
	interface IEngine {
		public static List<SearchResult> search(Program.QueryMetaPair token) =>
			throw new NotImplementedException();
	}
}
