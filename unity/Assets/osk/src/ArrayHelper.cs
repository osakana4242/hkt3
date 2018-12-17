namespace Osk42 {
	public static class ArrayHelper {
		public static int FindIndex<T1, T2>(T1[] self, T2 prm, System.Func<T1, T2, bool> f) {
			for (var i = 0; i < self.Length; i++) {
				var item = self[i];
				if (f(item, prm)) return i;
			}
			return -1;
		}
	}
}
