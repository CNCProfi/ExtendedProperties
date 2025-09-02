using System;

namespace com.audionysos.general.props.shorts {
	/// <summary>Provide convenient interface to create new initializers of common types.</summary>
	public class I {

		/// <summary></summary>
		public static StaticInitializer nitializer(params EPInitializer<EPInfo>[] initializers) {
			return new StaticInitializer(initializers);
		}

		/// <summary></summary>
		public static StaticInitializer nitializer(Action<EPInfo> a, params EPInitializer<EPInfo>[] initializers) {
			return new StaticInitializer(a, initializers);
		}

		/// <summary></summary>
		public static InstanceInitializer nstanceInitializer(params EPInitializer<EP>[] initializers) {
			return new InstanceInitializer(initializers);
		}

		/// <summary></summary>
		public static InstanceInitializer nstanceInitialize(Action<EP> a, params EPInitializer<EP>[] initializers) {
			return new InstanceInitializer(a, initializers);
		}

		/// <summary></summary>
		public static ICounter nstanceCounter(params EPInitializer<EP>[] initializers) {
			return new ICounter(initializers);
		}

	}
}
