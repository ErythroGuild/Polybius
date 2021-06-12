using System;
using System.Collections.Generic;

namespace Polybius.Engines {
	interface IEngine {
		public static List<SearchResult> search(SearchToken token) =>
			throw new NotImplementedException();
	}
}
