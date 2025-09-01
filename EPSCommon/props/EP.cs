using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace com.audionysos.general.props {

	/// <summary>Represents complete description of some parent's object property and is used as key element of extended property concept.
	/// Instances of this class suppose to be static variables shared across multiple instances of <see cref="EP{T}"/> (produced by creating new instances of parent object).
	/// </summary>
	public sealed class EPInfo {
		/// <summary>Owner type where this property is defined.</summary>
		public readonly Type owner;
		/// <summary>Address of the owner property used with reflection on owner type.</summary>
		public readonly string address;
		/// <summary>Short name of the property.</summary>
		public readonly string name;
		/// <summary>Few words about the property to help end user when he don't know nomenclature.</summary>
		public readonly string info;
		/// <summary>Detailed description about the property for end user - how, where, when to use it, how it relates to other properties of a system etc.</summary>
		public readonly string description;
		/// <summary>Constrains used when setting extended property created using this info object.
		/// Any change of in this constrains will affect all instances of the property even if those changes were made after property instantiation.</summary>
		public PConstrains constrains { get; private set; }
		/// <summary>Static initializer invoked when this instance where initialized.</summary>
		public readonly EPInitializer<EPInfo> initializer;
		/// <summary>Instance initializer invoked once after an instance of property this info describes is created.</summary>
		public readonly EPInitializer<EP> instanceInitializer;

		/// <summary>Indicate that static initializer is currently been invoked.</summary>
		private bool isInitializng = false;

		/// <summary>Creates new extended property info.</summary>
		/// <param name="owner">Type which owns described property.</param>
		/// <param name="address">Property name defined in owner type, usually acquired with <see cref="nameof"/> syntax.</param>
		/// <param name="staticInitializer">Static initializer invoked once after whole info abject is set.
		/// You can use existing initializers or write you own to perform any additional stuff needed when property is define.
		/// Static initializers should not be reused by different properties/<see cref="EPInfo"/> instances. If you want to share common setup, for many properties, created a method or property that is creating new instances of initializers.</param>
		/// <param name="name">Name of the property displayed to front-end user. If name is not specified, property address is used as a name.</param>
		/// <param name="info">Short info for front-end user about what this property represents.</param>
		/// <param name="description">Detailed description about property usage.</param>
		/// <param name="constrains">Constrains object applied each time an instance of the property is set to new value.</param>
		/// <param name="instanceInitializer">Initializer applied each time new instance of the property is set.</param>
		public EPInfo(Type owner, string address, EPInitializer<EPInfo> staticInitializer = null, string name = null, string info = null, string description = null, PConstrains constrains = null, EPInitializer<EP> instanceInitializer = null) {
			this.owner = owner;
			this.address = address;
			this.name = name ?? address;
			this.info = info;
			this.description = description;
			this.constrains = constrains;
			this.instanceInitializer = instanceInitializer;
			this.initializer = staticInitializer;

			isInitializng = true;
			initializer?.initialize(this);
			isInitializng = false;
		}

		/// <summary>This method is for use by static <see cref="initializer"/>, that is working with constrains and can be called if no explicit constrains were specified by the programmer to set the <see cref="constrains"/> property.
		/// This method cane be called only form <see cref="EPInitializer{T}.initialize(T)"/> method and if current constrain object is null, otherwise an exception will be thrown.</summary>
		/// <param name="cs">Constrains object to which set the property. If null given, base <see cref="PConstrains"/> type is used.</param>
		public PConstrains initalizeConstrains(PConstrains cs = null) {
			if (!isInitializng) throw new InvalidOperationException($@"The {nameof(initalizeConstrains)} method can be called only from initialize() method of the static initializer.");
			if (constrains != null) throw new InvalidOperationException("The constrains are already set.");
			constrains = cs ?? new PConstrains();
			return constrains;
		}

		/// <inheritdoc/>
		public override string ToString() {
			return $@"{owner.Name}.{address} [EPInfo]";
		}
	}

	/// <summary>Base class for generic extended property (see <see cref="EP{T}"/>) type.
	/// This class suppose to provide common access point for code that can mange different type of <see cref="EP{T}"/>
	/// were working with explicit types is problematic or not possible. 
	/// </summary>
	public abstract class EP {
		/// <summary>Static detailed information about the property, used across multiple instances of property parent object.</summary>
		public abstract EPInfo info { get; }

		/// <summary>This should return generic type of <see cref="EP{T}"/> instance.</summary>
		public abstract Type type { get; }

		/// <summary>The type of delegate that need to be passed to <see cref="listenEvent(Delegate, EPEvents)"/> and <see cref="muteEvent(Delegate, EPEvents)"/> methods.</summary>
		public abstract Type eventType { get; }

		/// <summary>Object which owns the property.
		/// This object may be required by some <see cref="PConstrains"/> but generally it's not mandatory for <see cref="EP"/> to have an owner so it can be null.
		/// To set this property, the owner object must call <see cref="provideFieldsOwner(object)"/> (typically in it's constructor).
		/// Constrains which require owner instance to operate should explicitly state that in the documentation.</summary>
		public object owner { get; private set; }

		/// <summary>If this instance is of <see cref="EP{T}"/> type an given argument are correct,
		/// this will create appropriate delegate from specified generic method that can be later used as events handler.</summary>
		/// <param name="methodParent">Parent object of method with given name.</param>
		/// <param name="methodName">Name of generic method used to create the delegate.
		/// This method must take 1 generic type parameter T" and one argument of type <see cref="EPEvent{T}"/> which will be an event instance.</param>
		public Delegate createEventHandler(object methodParent, string methodName) {
			var propertyType = GetType().GenericTypeArguments[0];
			var mi = methodParent.GetType().GetMethod(methodName);
			mi = mi.MakeGenericMethod(propertyType);
			return Delegate.CreateDelegate(eventType, methodParent, mi);
		}

		/// <summary>Assigns event listener of specified event type.
		/// To stop listening events use <see cref="muteEvent(Delegate, EPEvents)"/> method.
		/// Those methods allow you to listen for <see cref="EP{T}"/> using <see cref="EP"/> base class, were T is "unknown".
		/// If you work on specific type of <see cref="EP{T}"/>, use standard events approach.</summary>
		/// <param name="epEventHandler">Delegate to be invoked when event occurs. 
		/// This delegate must by of <see cref="eventType"/> type. Use <see cref="createEventHandler(object, string)"/> to create delegate from any applicable method.
		/// Basically this is an <see cref="EPEventHandler{T}"/> where T is the type of extended property.</param>
		/// <param name="type">Type of event on which to invoke given delegate.</param>
		public abstract void listenEvent(Delegate epEventHandler, EPEvents type);

		/// <summary>Remove event listener of specified event type.
		/// To start listening events use <see cref="listenEvent(Delegate, EPEvents)"/> method.
		/// Those methods allow you to listen for <see cref="EP{T}"/> using <see cref="EP"/> base class, were T is "unknown".
		/// If you work on specific type of <see cref="EP{T}"/>, use standard events approach.</summary>
		/// <param name="ePEventHandler">Delegate to be invoked when event occurs.
		/// This delegate must by of <see cref="eventType"/> type.
		/// Basically this is an <see cref="EPEventHandler{T}"/> where T is the type of extended property.</param>
		/// <param name="type">Type of event on which to invoke given delegate.</param>
		public abstract void muteEvent(Delegate ePEventHandler, EPEvents type);

		/// <summary>Sets <see cref="owner"/> to given object to all field of <see cref="EP"/> type. This method should be called only once per object instance.</summary>
		/// <param name="o"></param>
		public static void provideFieldsOwner(object o) {
			var t = o.GetType();
			while (t != null) {
				var fs = t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
				for (int i = 0; i < fs.Length; i++) {
					var f = fs[i];
					if (!typeof(EP).IsAssignableFrom(f.FieldType)) continue;
					var ep = f.GetValue(o) as EP;
					if (ep == null) return;
					if (ep.owner != null) throw new InvalidOperationException($@"Owner object was already set for the property of {o}.");
					ep.owner = o;
				}
				t = t.BaseType;
			}
		}

	}

	/// <summary>Extended property is wrapper for objects of any type which should be accessed publicly or cared with some uniform manner.
	/// This class suppose to simplify process of designing complex properties of an object by providing single entry point for all common property settings,
	/// allowing to confidently create any constrains for any values and specify property dependencies at point of property declaration, specify description for front-end users,
	/// allow to track property changes using events and design any other custom functionality shared across many properties.
	/// Main goals are to:
	/// -Reduce boilerplate to minimum.
	/// -Increase reliability of the program by minimizing amount of tedious, repetitive programming tasks and provide uniform interface for defining any unusual programming patters.
	/// An instance of <see cref="EP{T}"/> represents a single instance of a property. Each new parent object should have it's own <see cref="EP{T}"/> property instance.
	/// Extended property is basically composed of two main objects - a <see cref="value"/> of extended property generic type and <see cref="info"/> which holds any required information to process the data.
	/// Because of this duality, an extended property is created in two separate steps. First you create an instance of <see cref="EPInfo"/> describing the property, then you create instance of <see cref="EP{T}"/> providing that info in constructor.
	/// Info object of extended property is suppose to be static member of property's parent object an be shared across multiple instance of the property (when new parent type instances are created),
	/// but an info is passed to <see cref="EP{T}"/> constructor each time new extended property is created so it is also possible to have different descriptions of the same property for different instances of parent type.
	/// The info object is not only intended to passively describe some aspects of the property (as the name would suggest) like member Attributes does but it is an integral part of extended property
	/// and in fact any fancy stuff in done from <see cref="EPInfo"/>.
	/// 
	/// Initializers
	/// Extended properties use two types of initializers to perform any additional operations needed at property initialization.
	/// First is invoked only once after property info creation.
	/// Second is Invoked each time after new instance of extended property is created with given info.
	/// Both initializer are basically the same abstract types deriving from <see cref="EPInitializer{T}"/>.
	/// Only difference is the argument which is passed to them at initialization times. First one take <see cref="EPInfo"/> as input and the other <see cref="EP{T}"/>.
	/// Those initializers are completely abstract and defined by programmer. Extended properties don't use any initializers by default.
	/// 
	/// Constrains
	/// Constrains define what happen to variable when you access it.
	/// Unlike initializers, constrains are used all the way across the lifespan of the property and are applied each time property is suppose to be set to new value or be read.
	/// Constrains can be grouped and arranged in any way and their exact behavior and order of application is dependent on actual constrain's type. 
	/// Programmer can write and use he's own custom constrain and mix it with existing ones.
	/// Constrains can be very powerful and it is possible to get practically any desired output value using them but they should be used with care
	/// as they can also as easily lead to unwanted results and can impact on program performance significantly.
	/// 
	/// When you create info for extended property, provided static initializer is invoked
	/// </summary>
	public sealed class EP<T> : EP {

		/// <summary>Implements abstract method of <see cref="EP"/> base class.
		/// If you have access to specific <see cref="EP{T}"/> type use normal event handling approach.
		/// All events that could be accessible by this method here will be accessible publicly through <see cref="EP{Task}"/> API.
		/// See <see cref="EP.listenEvent(Delegate, EPEvents)"/> and <see cref="muteEvent(Delegate, EPEvents)"/> methods description for more details.</summary>
		public override void listenEvent(Delegate l, EPEvents type){
			if (type == EPEvents.CHANGED) CHANGED += l as EPEventHandler<T>;
		}

		/// <summary>Implements abstract method of <see cref="EP"/> base class.
		/// If you have access to specific <see cref="EP{T}"/> type use normal event handling approach.
		/// All events that could be accessible by this method here will be accessible publicly through <see cref="EP{T}"/> API.
		/// See <see cref="EP.listenEvent(Delegate, EPEvents)"/> and <see cref="muteEvent(Delegate, EPEvents)"/> methods description for more details.</summary>
		public override void muteEvent(Delegate l, EPEvents type) {
			if (type == EPEvents.CHANGED) CHANGED -= l as EPEventHandler<T>;
		}

		/// <summary>Dispatched when value of the property has changed.</summary>
		public event EPEventHandler<T> CHANGED;

		/// <summary>Current value of the property.</summary>
		private T _v;
		/// <summary>Current value of the property.</summary>
		public T value {
			get => _v;
			set {
				var i = _v;
				if(constrains) constrains.apply(value, ref _v, owner);
				else _v = value;
				if (i != null && !i.Equals(_v)) CHANGED?.Invoke(new EPEvent<T>(this));
			}
		}

		/// <summary>Static detailed information about the property, used across multiple instances of property parent object.</summary>
		private EPInfo _info;
		/// <summary>Static detailed information about the property, used across multiple instances of property parent object.</summary>
		public override EPInfo info => _info;

		/// <summary>Generic type of this instance.</summary>
		public override Type type => typeof(T);
		/// <summary><see cref="EPEventHandler{T}"/> where T is the same generic type of this <see cref="EP{T}"/> instance.</summary>
		public override Type eventType => typeof(EPEventHandler<T>);

		/// <summary>Constrains used when setting this property.
		/// This is shortcut for "info.constrians" and is shared across multiple instances of the property.</summary>
		private PConstrains constrains => _info.constrains;

		/// <summary>Creates new extended property instance.</summary>
		/// <param name="value"></param>
		/// <param name="info"></param>
		public EP(T value, EPInfo info) {
			_info = info;
			this.value = value;
			if (constrains) constrains.CHANGED += onConstraninsChanged;
			info.instanceInitializer?.initialize(this);
		}

		private void onConstraninsChanged(PConstrainEvent e) => value = _v;

		/// <summary>Searches for instance initializer of specified type.
		/// If no initializer of specified type were found, null is returned.</summary>
		public I getInstanceIN<I>() where I : EPInitializer<EP> {
			return info.instanceInitializer?.getInitializer<I>();
		}

		/// <summary>Searches for static initializer of specified type.
		/// If no initializer of specified type were found, null is returned.</summary>
		public I getInitializer<I>() where I : EPInitializer<EPInfo> {
			return info.initializer?.getInitializer<I>();
		}

		#region Operators

		/// <summary></summary>
		public static implicit operator T(EP<T> v) => v._v;

		/// <summary>Uses the operator of two properties by casting them to "dynamic" object.</summary>
		public static bool operator >(EP<T> v1, EP<T> v2) {
			return (dynamic)v1 > (dynamic)v2;
		}

		/// <summary>Uses the operator of two properties by casting them to "dynamic" object.</summary>
		public static bool operator <(EP<T> v1, EP<T> v2) {
			return (dynamic)v1 < (dynamic)v2;
		}

		/// <summary>Uses the operator of two properties by casting them to "dynamic" object.</summary>
		public static bool operator ==(EP<T> v1, EP<T> v2) {
			return (dynamic)v1 == (dynamic)v2;
		}

		/// <summary>Uses the operator of two properties by casting them to "dynamic" object.</summary>
		public static bool operator !=(EP<T> v1, EP<T> v2) {
			return (dynamic)v1 != (dynamic)v2;
		}

		/// <summary>Uses the operator of two properties by casting them to "dynamic" object.</summary>
		public static EP<T> operator -(EP<T> v1, EP<T> v2) {
			return (dynamic)v1 != (dynamic)v2;
		}

		/// <inheritdoc/>
		public override bool Equals(object obj) => obj is EP<T> e && e.value.Equals(value);
		/// <inheritdoc/>
		public override int GetHashCode() => value.GetHashCode();

		#endregion

		/// <summary>Creates property instance from a tuple.</summary>
		public static implicit operator EP<T>((T v, EPInfo i) t) => new EP<T>(t.v, t.i);

		/// <inheritdoc/>
		public override string ToString() {
			return $@"{_v} [EP]";
		}
	}

	/// <summary>Lists event types fired by instances of <see cref="EP{T}"/>
	/// This is to provide valid event type input to <see cref="EP{T}.listenEvent(Delegate, EPEvents)"/> or <see cref="EP{T}.muteEvent(Delegate, EPEvents)"/> methods.</summary>
	public enum EPEvents {
		/// <summary>Value of extended property was changed.</summary>
		CHANGED
	}

	/// <summary>Delegate used to handle <see cref="EP{T}"/> events.</summary>
	/// <typeparam name="T">Instance of event that occurred.</typeparam>
	public delegate void EPEventHandler<T>(EPEvent<T> e);

	/// <summary>Base, common class for <see cref="EPEvent{T}"/>.</summary>
	public abstract class EPEvent {
		internal abstract EP prop { get; } 
	}

	/// <summary>Represents any event raised by an <see cref="EP{T}"/> instance.</summary>
	public class EPEvent<T> : EPEvent {
		/// <summary>Property which created this event.</summary>
		public readonly EP<T> p;

		/// <summary></summary>
		public EPEvent(EP<T> property) {
			this.p = property;
		}

		/// <summary>prop => p</summary>
		internal override EP prop => p;
	}
}
