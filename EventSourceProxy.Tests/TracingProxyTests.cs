﻿using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;

namespace EventSourceProxy.Tests
{
	[TestFixture]
	public class TracingProxyTests : BaseLoggingTest
	{
		#region Test Classes
		public interface ICalculator
		{
			void Clear();
			int AddNumbers(int x, int y);
		}

		public class Calculator : ICalculator
		{
			public void Clear()
			{
			}

			public int AddNumbers(int x, int y)
			{
				return x + y;
			}
		}

		public class VirtualCalculator : ICalculator
		{
			public virtual void Clear()
			{
			}

			public virtual int AddNumbers(int x, int y)
			{
				return x + y;
			}
		}

		public class VirtualCalculatorWithoutInterface
		{
			public virtual void Clear()
			{
			}

			public virtual int AddNumbers(int x, int y)
			{
				return x + y;
			}
		}

		public interface ICalculatorWithCompleted
		{
			void Clear();
			int Clear_Completed();
			int AddNumbers(int x, int y);
			void AddNumbers_Completed(int result);
		}
		#endregion

		#region Create Proxy From Interface Tests
		[Test]
		public void TestLoggingProxyFromClassToInterface()
		{
			// create a logger for the interface and listen on it
			var logger = EventSourceImplementer.GetEventSource<ICalculator>();
			_listener.EnableEvents(logger, EventLevel.LogAlways, (EventKeywords)(-1));

			// create a calculator and a proxy
			var proxy = TracingProxy.Create<ICalculator>(new Calculator());

			// call the method through the proxy
			proxy.Clear();
			Assert.AreEqual(3, proxy.AddNumbers(1, 2));

			// look at the events in the log
			VerifyEvents(logger);
		}

		[Test]
		public void TestLoggingProxyFromVirtualClassToInterface()
		{
			// create a logger for the interface and listen on it
			var logger = EventSourceImplementer.GetEventSource<ICalculator>();
			_listener.EnableEvents(logger, EventLevel.LogAlways, (EventKeywords)(-1));

			// create a calculator and a proxy
			var proxy = TracingProxy.Create<ICalculator>(new VirtualCalculator());

			// call the method through the proxy
			proxy.Clear();
			Assert.AreEqual(3, proxy.AddNumbers(1, 2));

			// look at the events in the log
			VerifyEvents(logger);
		}

		[Test]
		public void TestLoggingProxyFromVirtualClassToVirtualClass()
		{
			var logger = EventSourceImplementer.GetEventSource<VirtualCalculatorWithoutInterface>();
			_listener.EnableEvents(logger, EventLevel.LogAlways, (EventKeywords)(-1));

			// create a calculator and a proxy
			var proxy = TracingProxy.Create<VirtualCalculatorWithoutInterface>(new VirtualCalculatorWithoutInterface());

			// call the method through the proxy
			proxy.Clear();
			Assert.AreEqual(3, proxy.AddNumbers(1, 2));

			VerifyEvents(logger);
		}

		[Test]
		public void TestLoggingProxyFromVirtualClassToUnrelatedInterface()
		{
			// create a logger for the interface and listen on it
			var logger = EventSourceImplementer.GetEventSource<ICalculator>();
			_listener.EnableEvents(logger, EventLevel.LogAlways, (EventKeywords)(-1));

			// create a calculator and a proxy
			var proxy = TracingProxy.Create<VirtualCalculatorWithoutInterface, ICalculator>(new VirtualCalculatorWithoutInterface());

			// call the method through the proxy
			proxy.Clear();
			Assert.AreEqual(3, proxy.AddNumbers(1, 2));

			// look at the events in the log
			VerifyEvents(logger);
		}

		[Test]
		public void TestLoggingProxyWithCompletedEvents()
		{
			// create a logger for the interface and listen on it
			var logger = EventSourceImplementer.GetEventSource<ICalculatorWithCompleted>();
			_listener.EnableEvents(logger, EventLevel.LogAlways, (EventKeywords)(-1));

			// create a calculator and a proxy
			var proxy = TracingProxy.Create<VirtualCalculator, ICalculatorWithCompleted>(new VirtualCalculator());

			// call the method through the proxy
			proxy.Clear();
			Assert.AreEqual(3, proxy.AddNumbers(1, 2));

			VerifyEvents(logger);

			// check the events
			var events = _listener.Events.ToArray();
			Assert.AreEqual(4, events.Length);
			var eventSource = events[0].EventSource;

			// check the individual events
			Assert.AreEqual(logger, events[0].EventSource);
			Assert.AreEqual(1, events[0].EventId);
			Assert.AreEqual(null, events[0].Message);
			Assert.AreEqual(EventLevel.Informational, events[0].Level);
			Assert.AreEqual((EventKeywords)1, events[0].Keywords);
			Assert.AreEqual(0, events[0].Payload.Count);

			Assert.AreEqual(logger, events[1].EventSource);
			Assert.AreEqual(2, events[1].EventId);
			Assert.AreEqual(null, events[1].Message);
			Assert.AreEqual(EventLevel.Informational, events[1].Level);
			Assert.AreEqual((EventKeywords)2, events[1].Keywords);
			Assert.AreEqual(0, events[0].Payload.Count);

			Assert.AreEqual(logger, events[2].EventSource);
			Assert.AreEqual(3, events[2].EventId);
			Assert.AreEqual(null, events[2].Message);
			Assert.AreEqual(EventLevel.Informational, events[2].Level);
			Assert.AreEqual((EventKeywords)4, events[2].Keywords);
			Assert.AreEqual(2, events[2].Payload.Count);
			Assert.AreEqual(1, events[2].Payload[0]);
			Assert.AreEqual(2, events[2].Payload[1]);

			// a fourth event for completed
			Assert.AreEqual(logger, events[3].EventSource);
			Assert.AreEqual(4, events[3].EventId);
			Assert.AreEqual(null, events[3].Message);
			Assert.AreEqual(EventLevel.Informational, events[3].Level);
			Assert.AreEqual((EventKeywords)8, events[3].Keywords);
			Assert.AreEqual(1, events[3].Payload.Count);
			Assert.AreEqual(3, events[3].Payload[0]);
		}

		private void VerifyEvents(object logger)
		{
			// check the events
			var events = _listener.Events.ToArray();
			Assert.AreEqual(4, events.Length);
			var eventSource = events[0].EventSource;

			// check the individual events
			Assert.AreEqual(logger, events[0].EventSource);
			Assert.AreEqual(1, events[0].EventId);
			Assert.AreEqual(null, events[0].Message);
			Assert.AreEqual(EventLevel.Informational, events[0].Level);
			Assert.AreEqual((EventKeywords)1, events[0].Keywords);
			Assert.AreEqual(0, events[0].Payload.Count);

			Assert.AreEqual(logger, events[1].EventSource);
			Assert.AreEqual(2, events[1].EventId);
			Assert.AreEqual(null, events[1].Message);
			Assert.AreEqual(EventLevel.Informational, events[1].Level);
			Assert.AreEqual(0, events[0].Payload.Count);

			Assert.AreEqual(logger, events[2].EventSource);
			Assert.AreEqual(3, events[2].EventId);
			Assert.AreEqual(null, events[2].Message);
			Assert.AreEqual(EventLevel.Informational, events[2].Level);
			Assert.AreEqual(2, events[2].Payload.Count);
			Assert.AreEqual(1, events[2].Payload[0]);
			Assert.AreEqual(2, events[2].Payload[1]);

			// a fourth event for completed
			Assert.AreEqual(logger, events[3].EventSource);
			Assert.AreEqual(4, events[3].EventId);
			Assert.AreEqual(null, events[3].Message);
			Assert.AreEqual(EventLevel.Informational, events[3].Level);
			Assert.AreEqual(1, events[3].Payload.Count);
			Assert.AreEqual(3, events[3].Payload[0]);
		}
		#endregion

		#region Automatic Activity ID Tests
		public class AutomaticActivity
		{
			public Guid ActivityId { get; set; }

			public virtual void Method()
			{
				ActivityId = EventActivityScope.CurrentActivityId;
			}

			public virtual void Throws()
			{
				throw new Exception();
			}
		}

		[Test]
		public void MethodInterfaceShouldCreateActivity()
		{
			Assert.AreEqual(Guid.Empty, EventActivityScope.CurrentActivityId);

			var tester = new AutomaticActivity();
			var proxy = TracingProxy.Create<AutomaticActivity>(tester);
			proxy.Method();

			Assert.AreNotEqual(Guid.Empty, tester.ActivityId);

			Assert.AreEqual(Guid.Empty, EventActivityScope.CurrentActivityId);
		}

		[Test]
		public void MethodInterfaceShouldNotChangeActivity()
		{
			Assert.AreEqual(Guid.Empty, EventActivityScope.CurrentActivityId);

			using (EventActivityScope scope = new EventActivityScope())
			{
				Assert.AreNotEqual(Guid.Empty, EventActivityScope.CurrentActivityId);

				var tester = new AutomaticActivity();
				var proxy = TracingProxy.Create<AutomaticActivity>(tester);
				proxy.Method();

				Assert.AreEqual(scope.ActivityId, tester.ActivityId);
				Assert.AreEqual(scope.ActivityId, EventActivityScope.CurrentActivityId);
			}

			Assert.AreEqual(Guid.Empty, EventActivityScope.CurrentActivityId);
		}

		[Test]
		public void MethodThatThrowsShouldUnwindActivity()
		{
			Assert.AreEqual(Guid.Empty, EventActivityScope.CurrentActivityId);

			var tester = new AutomaticActivity();
			var proxy = TracingProxy.Create<AutomaticActivity>(tester);
			try
			{
				proxy.Throws();
			}
			catch
			{
			}

			Assert.AreEqual(Guid.Empty, EventActivityScope.CurrentActivityId);
		}
		#endregion
	}
}
