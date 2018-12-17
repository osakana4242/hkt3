namespace Osk42 {
	public static class MathHelper {
		public static bool isIn(int v, int min, int max) {
			return min <= v && v < max;
		}
		public static bool isIn(float v, float min, float max) {
			return min <= v && v < max;
		}
	}
}
