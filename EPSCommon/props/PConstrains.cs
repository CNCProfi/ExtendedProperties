using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using com.audionysos.general.extensions;

namespace com.audionysos.general.props {

	/// <summary>Base class for property constrains sets. This simply collects any property constrains and execute them on by one when <see cref="apply{T}(T, ref T)"/> is called.
	/// The <see cref="PConstrain.correct{T}(T, ref T, object)"/> method is called in both cases - if <see cref="PConstrain.test(object, object)"/> result is <see cref="PCostrainStatus.PASSED"/> or <see cref="PCostrainStatus.CORRECTED"/>.
	/// Otherwise, the property value is left unchanged.</summary>
	public class PConstrains : PConstrain, IReadOnlyList<PConstrain>, IEnumerable<PConstrain> {

		/// <summary>List of all specified constrains.</summary>
		protected List<PConstrain> list = new List<PConstrain>();
		/// <summary>List of pending constrain waiting to be tested again.</summary>
		internal readonly List<PConstrain> pending = new List<PConstrain>();

		/// <summary>Adds new sub constrain to the set.</summary>
		public void Add(PConstrain c) {
			list.Add(c);
			c.CHANGED += onConstrianChanged;
		}

		private void onConstrianChanged(PConstrainEvent e) => fireChangeEvent();

		/// <inheritdoc/>
		public int Count => list.Count;

		/// <summary>Apply the value to given property considering all specified constrains.</summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="value">New value that should be applied.</param>
		/// <param name="property">Current property where value should be set.</param>
		/// <param name="owner">Property's owner object instance. This argument is not always available, and need to be explicitly provided by the owner (see <see cref="EP.owner"/>).</param>
		/// <returns></returns>
		public virtual PConstrianResults apply<T>(T value, ref T property, object owner) {
			var r = new PConstrianResults(list.Count);
			for (int i = 0; i < list.Count; i++) { var c = list[i];
				var tr = c.test(value, owner);
				if (tr.status == PCostrainStatus.PASSED ||
					tr.status == PCostrainStatus.CORRECTED)
					c.correct(value, ref property, owner);
				else pending.Add(c);
			}
			var pc = 1;
			do {
				pc = pending.Count;
				for (int i = 0; i < pending.Count; i++) { var c = pending[i];
					var tr = c.test(value, owner);
					if (tr.status == PCostrainStatus.REFUSED) continue;
					c.correct(value, ref property, owner);
					pending.RemoveAt(i); i--;
				}
			} while (pc > 0 && pc != pending.Count);

			return r;
		}

		/// <inheritdoc/>
		public override PConstrianResult test(object newValue, object owner) {
			var r = new PConstrianResults(list.Count);
			for (int i = 0; i < list.Count; i++) { var c = list[i];
				r.Add(c.test(newValue, owner));
			} return r;
		}

		/// <inheritdoc/>
		public override PConstrianResult correct<T>(T value, ref T current, object owner) {
			return apply(value, ref current, owner);
		}

		/// <inheritdoc/>
		public IEnumerator GetEnumerator() {
			return list.GetEnumerator();
		}

		/// <inheritdoc/>
		IEnumerator<PConstrain> IEnumerable<PConstrain>.GetEnumerator()
			=> list.GetEnumerator();

		/// <inheritdoc/>
		public PConstrain this[int i] { get => list[i]; }
	}

	/// <summary>Applies first valid constrain. All other constrains are ignored.
	/// If none of constrains is valid but input value can be corrected, the one with greatest correctness value will be chosen.</summary>
	public class FirstValid : PConstrains {

		/// <inheritdoc/>
		public override PConstrianResults apply<T>(T value, ref T property, object owner) {
			var r = new PConstrianResults(list.Count);
			var bc = int.MinValue; var bi = 0; //best correctness and it's index
			for (int i = 0; i < list.Count; i++) { var c = list[i];
				var tr = c.test(value, owner); r.Add(tr);
				if (tr.status == PCostrainStatus.PASSED) { bi = i; break; }
				if (tr.status == PCostrainStatus.CORRECTED &&
					tr.correctnes > bc) { bc = tr.correctnes; bi = i; }
			}
			list[bi].correct(value, ref property, owner);
			return r;
		}
	}

	/// <summary>Base class to all constrains that could be set on extended property (<see cref="EP"/>).</summary>
	public abstract class PConstrain {
		/// <summary>Event dispatched when constrain (it's parameters) itself was changed.</summary>
		public event PConstrainEventHandler CHANGED;

		/// <summary></summary>
		public PConstrain() {}

		/// <summary>Dispatches <see cref="CHANGED"/> event so that extended property can reexamine constrains.</summary>
		protected void fireChangeEvent() => CHANGED?.Invoke(new PConstrainEvent(this));

		/// <summary>Correct and set new value for given current value.</summary>
		/// <param name="value">New value to be set.</param>
		/// <param name="current">Current value.</param>
		/// <param name="owner">Property's owner object instance. This argument is not always available, and need to be explicitly provided by the owner (see <see cref="EP.owner"/>).</param>
		/// <returns>Result of correction.</returns>
		public abstract PConstrianResult correct<T>(T value, ref T current, object owner);

		/// <summary>Test if given value can be set to the property.</summary>
		/// <param name="newValue"></param>
		/// <param name="owner">Property's owner object instance. This argument is not always available, and need to be explicitly provided by the owner (see <see cref="EP.owner"/>).</param>
		/// <returns></returns>
		public abstract PConstrianResult test(object newValue, object owner);

		/// <summary>False if null.</summary>
		public static implicit operator bool(PConstrain c) => c != null;
	}

	/// <summary>Delegate that is able to correct given new value and set it as current one.</summary>
	/// <param name="value">New, requested value.</param>
	/// <param name="current">Current value of a property to which change should be applied.</param>
	public delegate PConstrianResult ConstrainCorector<CT>(CT value, ref CT current, object owner);
	/// <summary>Delegate which test given newValue of a property and returns appropriate <see cref="PConstrianResult"/>.
	/// If newValue is accepted by constrain without correcting it, the method should return result with <see cref="PCostrainStatus.PASSED"/> status.</summary>
	public delegate PConstrianResult ConstrainTester<CT>(object newValue, object owner);

	/// <summary>Custom dynamic constrain. This can be used to specify constrain without implementing a new constrain class, by simply passing some constrain delegates.
	/// A programmer can also write it's own constrain type by extending <see cref="PConstrain"/> class.</summary>
	public class CConstrian<T> : PConstrain {

		private ConstrainTester<T> tester;
		private ConstrainCorector<T> corrector;

		/// <summary></summary>
		/// <param name="tester">Tests each new value to which property suppose to be set without actual modification.</param>
		/// <param name="corrector">Sets actual value of the property.</param>
		public CConstrian(ConstrainTester<T> tester, ConstrainCorector<T> corrector) {
			this.tester = tester;
			this.corrector = corrector;
		}

		/// <inheritdoc/>
		public override PConstrianResult correct<IT>(IT value, ref IT current, object owner) {
			var c = (T)(object)current;
			var r = corrector?.Invoke((T)(object)value, ref c, owner);
			current = (IT)(object)c; return r;
		}

		/// <inheritdoc/>
		public override PConstrianResult test(object newValue, object owner) {
			return tester.Invoke(newValue, owner);
		}
	}

	/// <summary>Represents handler of <see cref="PConstrain"/> events.</summary>
	public delegate void PConstrainEventHandler(PConstrainEvent e);

	/// <summary>Represents event of a <see cref="PConstrain"/>.</summary>
	public class PConstrainEvent{
		/// <summary>Constrain associated with the event.</summary>
		public readonly PConstrain c;
		/// <summary></summary>
		public PConstrainEvent(PConstrain c) => this.c = c;
	}




	/// <summary>Represents result of a <see cref="PConstrain"/> application.</summary>
	public class PConstrianResult {
		internal int _c;
		/// <summary>Correctness value indicate how close to the correct/allowed value, given new value is.
		/// This is used for example by <see cref="FirstValid"/> constrain to indicate which constrain should be used to correct given value.</summary>
		public int correctnes => _c;
		/// <summary>Sets <see cref="correctnes"/> value and returns this result.</summary>
		public PConstrianResult setCore(int c) { _c = c; return this; }

		/// <summary>Status of constrains.</summary>
		protected PCostrainStatus _status;
		/// <summary>Status of constrains.</summary>
		public PCostrainStatus status => _status;

		/// <summary>The object or property which caused correction or refuse of input value.</summary>
		protected object _cause;
		/// <summary>The object or property which caused correction or refuse of input value.</summary>
		public object cause => _cause;

		protected string _info;
		public string info => _info;

		/// <summary></summary>
		/// <param name="status"></param>
		/// <param name="cause"></param>
		/// <param name="info"></param>
		public PConstrianResult(PCostrainStatus status, PConstrianResult cause = null, string info = null) {
			this._status = status;
			this._cause = cause;
			this._info = info;
		}

		/// <inheritdoc/>
		public override string ToString() {
			return $@"{_status} ({_c}) {_info}";
		}

		/// <summary>False if null.</summary>
		public static implicit operator bool(PConstrianResult r) => r!=null;
	}

	/// <summary>Contains a list of nested constrains results.
	/// Status is set to reflect most restricted status of inner result i.e. if any of results is <see cref="PCostrainStatus.REFUSED"/> status of this collection will also be refused.</summary>
	public class PConstrianResults : PConstrianResult, IEnumerable{
		private readonly List<PConstrianResult> all;

		/// <summary></summary>
		/// <param name="capcity">Initial capacity for nested results.</param>
		public PConstrianResults(int capcity)
			:base(PCostrainStatus.PASSED, null, "Initialized") {
			all = new List<PConstrianResult>(capcity);
		}

		/// <summary>Adds new nested result.</summary>
		public void Add(PConstrianResult r) {
			all.Add(r);
			if (//_status != PCostrainStatus.REFUSED &&
				r.status == PCostrainStatus.CORRECTED)
				{ _status = r.status; _info = "Corrected by inner constrain."; }
			else if (r.status == PCostrainStatus.REFUSED)
				{ _status = r.status; _info = "Refused by inner constrain."; } 
		}

		/// <inheritdoc/>
		public IEnumerator GetEnumerator() => all.GetEnumerator();

		/// <summary>False if null.</summary>
		public static implicit operator bool(PConstrianResults r) => r != null;
	}

	/// <summary>Enumerates possible states of result of applying <see cref="PConstrain"/>.</summary>
	public enum PCostrainStatus {
		/// <summary>Constrain test has passed successfully and given input value is correct.</summary>
		PASSED,
		/// <summary>Constrain not allow given input but it can be corrected to allowed value.</summary>
		CORRECTED,
		/// <summary>Constrain not allow for given input and cannot correct it.</summary>
		REFUSED
	}
}
