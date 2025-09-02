using System;
using System.Collections;
using System.Collections.Generic;

namespace com.audionysos.general.props {

	/// <summary>EPInitializer is designed for use with <see cref="EP{T}"/> but it's independent of it.
	/// Initializer task is trivial - collect child initializers, run any own code when called and call other initializers to do the same.
	/// Additionally base initializer can run some custom action specified in constructor.
	/// Inheritors suppose to override <see cref="initialize(T)"/> method, as base class only action given in constructor and call its children initializers.</summary>
	/// <typeparam name="T">Initialization target object's type.</typeparam>
	public abstract class EPInitializer<T> : IEnumerable {
		/// <summary>List of children initializers.</summary>
		protected List<EPInitializer<T>> list = new List<EPInitializer<T>>();
		/// <summary>Action to be performed when <see cref="initialize(T)"/> is called.</summary>
		protected Action<T> action;

		/// <summary></summary>
		/// <param name="l"></param>
		public EPInitializer(params EPInitializer<T>[] l) {
			list.AddRange(l);
		}

		/// <summary>Create new initializer with some custom action that will be invoked before other children initializers.</summary>
		/// <param name="a">Action to be invoked first</param>
		/// <param name="l">List of child initializers to invoked when <see cref="initialize(T)"/> method is called.</param>
		public EPInitializer(Action<T> a, params EPInitializer<T>[] l) {
			list.AddRange(l); action = a;
		}

		/// <summary>Run this initializer and it's children initializers.</summary>
		public virtual void initialize(T eP) {
			action?.Invoke(eP);
			for (int i = 0; i < list.Count; i++) list[i].initialize(eP);
		}

		/// <summary>Searches for initializer of specified type.
		/// If no initializer is found, null is returned.</summary>
		/// <typeparam name="SI"></typeparam>
		public SI getInitializer<SI>() where SI : EPInitializer<T> {
			if (this is SI) return this as SI;
			for (int i = 0; i < list.Count; i++)
				if (list[i] is SI) return (SI)list[i];
			for (int i = 0; i < list.Count; i++) {
				var r = list[i].getInitializer<SI>();
				if (r) return r;}
			return null;
		}

		/// <summary>Adds new child initializer to end of the list.</summary>
		public void Add(EPInitializer<T> initializer) => list.Add(initializer);
		/// <inheritdoc/>
		public IEnumerator GetEnumerator() => list.GetEnumerator();
		/// <inheritdoc/>
		public int Count => list.Count;
		/// <inheritdoc/>
		public EPInitializer<T> this[int i] { get => list[i]; }
		/// <summary>False if null.</summary>
		public static implicit operator bool(EPInitializer<T> i) => i!=null;
	}

	/// <summary>Do-nothing container for grouping other initializers.</summary>
	public class InstanceInitializer: EPInitializer<EP> {
		/// <summary></summary>
		public InstanceInitializer(params EPInitializer<EP>[] l):base(l) {}
		/// <summary></summary>
		public InstanceInitializer(Action<EP> a, params EPInitializer<EP>[] l):base(a,l) {}
	}

	/// <summary>Do-nothing container for grouping other initializers.</summary>
	public class StaticInitializer : EPInitializer<EPInfo> {
		/// <summary></summary>
		public StaticInitializer(params EPInitializer<EPInfo>[] l) : base(l) { }
		/// <summary></summary>
		public StaticInitializer(Action<EPInfo> a, params EPInitializer<EPInfo>[] l) : base(a, l) { }
	}

	/// <summary>This initializer counts number of instances created with it's parent <see cref="EPInfo"/> object.</summary>
	public class ICounter : InstanceInitializer {
		/// <summary>Counted number of instances.</summary>
		public int c { private set; get; }
		/// <inheritdoc/>
		public override void initialize(EP eP) {
			c++;
			base.initialize(eP);
		}
		/// <summary></summary>

		public ICounter(params EPInitializer<EP>[] l) : base(l) { }
		/// <summary></summary>
		public ICounter(Action<EP> a, params EPInitializer<EP>[] l) : base(a, l) { }
	}

}
