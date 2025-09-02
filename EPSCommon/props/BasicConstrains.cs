using com.audionysos.general.extensions;
using System;
using System.Collections.Generic;
using System.Text;

namespace com.audionysos.general.props {
	/// <summary>Constrain the value to specified bounds.
	/// Any type which contains (less than, greater than, equal and minus operator) can be used as generic type.
	/// If type is wrong, runtime error will be thrown.</summary>
	/// <typeparam name="T"></typeparam>
	public class Bounds<T> : PConstrain {
		/// <summary>Lower bound of property value.</summary>
		public readonly T min;
		/// <summary>Upper bound of property value.</summary>
		public readonly T max;
		/// <summary>Tells if lower bound value (<see cref="min"/>) should be allowed as well, or only bigger values should be accepted.</summary>
		public readonly bool includeMin;
		/// <summary>Tells if upper bound value (<see cref="max"/>) should be allowed as well, or only smaller values should be accepted.</summary>
		public readonly bool includeMax;

		//TODO: Switch sub/add bounds values to T type.
		/// <summary>Value to be subtracted from bound value in case the bound itself is not included.</summary>
		public int sub = 1;
		/// <summary>Value to be add to bound value in case the bound itself is not included.</summary>
		public int add = 1;

		/// <summary>Creates new bounds constrain</summary>
		/// <param name="min">Minimal value that can be set.</param>
		/// <param name="max">Maximal value that can be set.</param>
		/// <param name="includeMin">Specifies that a value can take given minimal value. If false, only smaller values are allowed.</param>
		/// <param name="includeMax">Specifies that a value can take given maximal value. If false, only greater values are allowed.</param>
		public Bounds(T min, T max, bool includeMin = true, bool includeMax = true) {
			var t = typeof(T);
			if (t.IsSubclassOf(typeof(EP))) {
				var d = (min as EP).createEventHandler(this, nameof(onBoundsChanged));

				//var pType = t.GenericTypeArguments[0];
				//var mi = this.GetType().GetMethod(nameof(onBoundsChanged));
				//mi = mi.MakeGenericMethod(pType);
				//var d = Delegate.CreateDelegate((min as EP).eventType, this, mi);
				t = (min as EP).type;
				(min as EP).listenEvent(d, EPEvents.CHANGED);
				(max as EP).listenEvent(d, EPEvents.CHANGED);
			}
			if (!t.hasOperators("<", ">", "==")) throw new Exception("Type of value don't support required operators (<,>,==).");
			if ((!includeMax || !includeMin) && !min.isNumber()) throw new Exception("min and max values can be only excluded from range if value type is numeric type.");
			this.min = min;
			this.max = max;
			this.includeMin = includeMin;
			this.includeMax = includeMax;
		}

		private void onBoundsChanged<ET>(EPEvent<ET> e) {
			fireChangeEvent();
		}

		private PConstrainResult inRange = new PConstrainResult(
			PConstrainStatus.PASSED, null, null);
		private PConstrainResult toBig = new PConstrainResult(
			PConstrainStatus.CORRECTED, null, "Given value is to big.");
		private PConstrainResult toSmall = new PConstrainResult(
			PConstrainStatus.CORRECTED, null, "Given value is to small.");
		private PConstrainResult result;

		/// <inheritdoc/>
		public override PConstrainResult test(object nv, object owner) {
			var xd = (dynamic)max - (dynamic)nv;
			var maxOk = xd > 0 || (includeMax && xd == 0);
			if (!maxOk) { result = toBig.setCore((int)xd); return toBig; }

			var nd = (dynamic)nv - (dynamic)min;
			var minOk = nd > 0 || (includeMin && nd == 0);
			if (!minOk) { result = toSmall.setCore((int)nd); return toSmall; }

			result = inRange; return inRange;
		}

		//public override PConstrianResult test(object nv) {
		//	var md = (dynamic)max - (dynamic)nv;
		//	var maxOk = ((dynamic)nv < (dynamic)max) ||
		//		(includeMax && ((dynamic)nv == (dynamic)max));
		//	if (!maxOk) { result = toBig; return toBig; }

		//	var minOk = ((dynamic)nv > (dynamic)min) ||
		//		(includeMin && ((dynamic)nv == (dynamic)min));
		//	if (!minOk) { result = toSmall; return toSmall; }

		//	result = inRange; return inRange;
		//}

		/// <inheritdoc/>
		public override PConstrainResult correct<M>(M value, ref M c, object owner) {
			if (!result) test(value, owner);
			if (result == inRange) c = value;
			else if (result == toBig) c = (includeMax) ? max : Math.Round((dynamic)max - sub);
			else if (result == toSmall) c = (includeMin) ? min : Math.Round((dynamic)min + add);
			var r = result; result = null; return r;
		}

		private double nextSmaller(double v) {
			var c = double.Epsilon;
			var r = v - c;
			while (r == v) {
				c += double.Epsilon;
				r = v - c;
			}
			return r;
		}

		/// <inheritdoc/>
		public override string ToString() {
			var s = "Bounds";
			s += ((includeMin) ? "<" : "(") + min.ToString() + ", ";
			s += max.ToString() + ((includeMax) ? ">" : ")");
			return s;
		}

	}

	/// <summary>Snaps input to closest value form specified list</summary>
	/// <typeparam name="T"></typeparam>
	public class SnapTo<T> : PConstrain {
		private T[] list;

		/// <summary></summary>
		/// <param name="list">List of values to which the property should be snapped.</param>
		public SnapTo(params T[] list) {
			this.list = list;
		}

		/// <inheritdoc/>
		public override PConstrainResult correct<T1>(T1 value, ref T1 current, object owner) {
			if (!result) test(value, owner);
			current = (T1)(object)list[ci];
			var r = result; result = null;
			return r;
		}

		/// <summary>Closest index.</summary>
		private int ci = -1;
		private PConstrainResult result;
		/// <inheritdoc/>
		public override PConstrainResult test(object newValue, object owner) {
			ci = -1; var bc = int.MinValue; result = null;
			for (int i = 0; i < list.Length; i++) {
				var av = list[i];
				if (newValue.Equals(av)) {
					result = new PConstrainResult(PConstrainStatus.PASSED);
					bc = int.MaxValue;
					ci = i; break;
				} else {
					var c = -Math.Abs((double)newValue - (double)(object)av);
					if (bc < c) { bc = (int)c; ci = i; }
				}
			}
			if (!result) result = new PConstrainResult(PConstrainStatus.CORRECTED);
			result.setCore(bc);
			return result;
		}
	}
}
