﻿using System;
using System.Collections.Generic;
using System.Linq;
using EventSourceProxy;
using NUnit.Framework;

namespace EventSourceProxy.Tests
{
    [TestFixture]
    public class FluentInterfaceTests : BaseLoggingTest
	{
		#region Test Interface
		public interface IEmailer
		{
			void Send(IEmail email, DateTime when);
			void Receive(IEmail email);
			void OtherName(IEmail othername);
		}

		interface INotEmail
		{
			void Nothing();
		}

		public interface IEmail
		{
			string From { get; }
			string To { get; }
			string Subject { get; }
			string Body { get; }

			IEnumerable<Byte[]> Attachments { get; }
		}

		public class EMail : IEmail
		{
			public EMail(string from, string to, string subject, string body, IEnumerable<Byte[]> attachements = null)
			{
				From = from;
				To = to;
				Subject = subject;
				Body = body;
				Attachments = attachements;
			}

			public string From { get; private set; }
			public string To { get; private set; }
			public string Subject { get; private set; }
			public string Body { get; private set; }
			public IEnumerable<Byte[]> Attachments { get; private set; }
		}

		public class OtherClass
		{
		}
		#endregion

		#region Everything Bagel Tests
		[Test]
		public void TestEverythingTogether()
		{
			var tpp = new TraceParameterProvider();
			tpp
				.For<IEmailer>(m => m.Send(Any<IEmail>.Value, Any<DateTime>.Value))
					.With<IEmail>()
						.Trace(e => e.From).As("sender")
						.Trace(e => e.To).As("recipient")
						.Trace(e => e.Subject).As("s")
							.And(e => e.Body).As("b")
							.TogetherAs("message")
						.Trace(e => String.Join("/", e.Attachments.Select(Convert.ToBase64String).ToArray()))
							.As("attachments")
					.EndWith()
					.Trace("when")
				.ForAnything()
					.AddContext("context", () => "testing");

			var proxy = (IEmailer)new TypeImplementer(typeof(IEmailer), tpp).EventSource;
			EnableLogging(proxy);
			proxy.Send(new EMail("from", "to", "subject", "body", new byte[][] { new byte[] { 1 } }), DateTime.Parse("1/1/2000"));

			// look at the events again
			var events = _listener.Events.ToArray();
			Assert.AreEqual(1, events.Length);

			var payload = events[0].Payload.Select(o => o.ToString()).ToArray();

			Assert.AreEqual(6, payload.Length);
			Assert.That(payload.Contains("from"));
			Assert.That(payload.Contains("to"));
			Assert.That(payload.Contains("{\"s\":\"subject\",\"b\":\"body\"}"));
			Assert.That(payload.Contains("AQ=="));
			Assert.That(payload.Contains("1/1/2000 12:00:00 AM"));
			Assert.That(payload.Contains("testing"));
		}
		#endregion

		#region For Method Filter Tests
		[Test]
		public void ForAny()
		{
			var traceDescription = new TraceParameterProvider().ForAnything();

			ParameterBuilder traceBuilder = (ParameterBuilder)traceDescription;

			Assert.True(traceBuilder.Matches(typeof(IEmailer).GetMethod("Send")));
			Assert.True(traceBuilder.Matches(typeof(IEmailer).GetMethod("Receive")));
			Assert.True(traceBuilder.Matches(typeof(INotEmail).GetMethod("Nothing")));
		}

		[Test]
		public void ForInterface()
		{
			var traceDescription = new TraceParameterProvider().For<IEmailer>();

			ParameterBuilder traceBuilder = (ParameterBuilder)traceDescription;

			Assert.True(traceBuilder.Matches(typeof(IEmailer).GetMethod("Send")));
			Assert.True(traceBuilder.Matches(typeof(IEmailer).GetMethod("Receive")));
			Assert.False(traceBuilder.Matches(typeof(INotEmail).GetMethod("Nothing")));
		}

		[Test]
		public void ForMethod()
		{
			var traceDescription = new TraceParameterProvider().For<IEmailer>(e => e.Send(Any<IEmail>.Value, Any<DateTime>.Value));

			ParameterBuilder traceBuilder = (ParameterBuilder)traceDescription;

			Assert.True(traceBuilder.Matches(typeof(IEmailer).GetMethod("Send")));
			Assert.False(traceBuilder.Matches(typeof(IEmailer).GetMethod("Receive")));
			Assert.False(traceBuilder.Matches(typeof(INotEmail).GetMethod("Nothing")));
		}
		#endregion

		#region Single Parameter Tests
		[Test]
		public void TraceParameterName()
		{
			var traceDescription = new TraceParameterProvider().For<IEmailer>()
				.Trace("email");

			ParameterBuilder traceBuilder = (ParameterBuilder)traceDescription;

			Assert.That(traceBuilder.Alias, Is.EqualTo("email"));

			var traceValues = traceBuilder.Values.ToArray();
			Assert.That(traceValues.Length, Is.EqualTo(1));
			ValidateValue(traceValues[0], "email", "email", false);
		}

		[Test]
		public void TraceParameterNameAs()
		{
			var traceDescription = new TraceParameterProvider().For<IEmailer>()
				.Trace("email")
					.As("message");

			ParameterBuilder traceBuilder = (ParameterBuilder)traceDescription;

			Assert.That(traceBuilder.Alias, Is.EqualTo("message"));

			var traceValues = traceBuilder.Values.ToArray();
			Assert.That(traceValues.Length, Is.EqualTo(1));
			ValidateValue(traceValues[0], "message", "email", false);
		}

		[Test]
		public void TraceParameterExpression()
		{
			var traceDescription = new TraceParameterProvider().For<IEmailer>()
				.With<IEmail>()
					.Trace(email => email.From)
				.EndWith();

			ParameterBuilder traceBuilder = (ParameterBuilder)traceDescription;

			Assert.That(traceBuilder.Alias, Is.EqualTo("From"));

			var traceValues = traceBuilder.Values.ToArray();
			Assert.That(traceValues.Length, Is.EqualTo(1));
			ValidateValue(traceValues[0], "From", null, true);
		}

        [Test]
        public void TraceParameterExpressionAndName()
        {
			var traceDescription = new TraceParameterProvider().For<IEmailer>()
				.With<IEmail>()
					.Trace("email", email => email.From)
				.EndWith();

			ParameterBuilder traceBuilder = (ParameterBuilder)traceDescription;

			Assert.That(traceBuilder.Values.First().Matches(typeof(IEmailer).GetMethod("Send").GetParameters()).Count(), Is.EqualTo(1));
			Assert.That(traceBuilder.Values.First().Matches(typeof(IEmailer).GetMethod("OtherName").GetParameters()).Count(), Is.EqualTo(0));

			Assert.That(traceBuilder.Alias, Is.EqualTo("email"));

			var traceValues = traceBuilder.Values.ToArray();
			Assert.That(traceValues.Length, Is.EqualTo(1));
			ValidateValue(traceValues[0], "From", "email", true);
        }

		[Test]
		public void TraceParameterMemberAs()
		{
			var traceDescription = new TraceParameterProvider().For<IEmailer>()
				.With<IEmail>()
					.Trace(email => email.From)
					.As("sender")
				.EndWith();

			ParameterBuilder traceBuilder = (ParameterBuilder)traceDescription;
			Assert.That(traceBuilder.Alias, Is.EqualTo("sender"));

			var traceValues = traceBuilder.Values.ToArray();
			Assert.That(traceValues.Length, Is.EqualTo(1));
			ValidateValue(traceValues[0], "sender", null, true);
		}

		[Test]
        public void TraceParameterExpressionAs()
        {
			var traceDescription = new TraceParameterProvider().For<IEmailer>()
				.With<IEmail>()
					.Trace(email => string.Join("/", email.Attachments.Select(Convert.ToBase64String).ToArray()))
					.As("attachments")
				.EndWith();

			ParameterBuilder traceBuilder = (ParameterBuilder)traceDescription;
			Assert.That(traceBuilder.Alias, Is.EqualTo("attachments"));

			var traceValues = traceBuilder.Values.ToArray();
            Assert.That(traceValues.Length, Is.EqualTo(1));
			ValidateValue(traceValues[0], "attachments", null, true);
        }
		#endregion

		#region Multiple Traces Tests
		[Test]
		public void TwoNameTraces()
		{
			var tpp = new TraceParameterProvider();
			tpp.For<IEmailer>()
				.Trace("email")
				.Trace("when");

			var builders = tpp.Builders.ToArray();
			Assert.That(builders.Length, Is.EqualTo(2));

			ValidateValue(builders[0].Values.First(), "email", "email", false);
			ValidateValue(builders[1].Values.First(), "when", "when", false);
		}

		[Test]
        public void TwoTraces()
        {
			var tpp = new TraceParameterProvider();
			tpp.For<IEmailer>()
				.With<IEmail>()
					.Trace(email => email.From)
					.Trace(email => email.To)
				.EndWith();

			var builders = tpp.Builders.ToArray();
			Assert.That(builders.Length, Is.EqualTo(2));

			ValidateValue(builders[0].Values.First(), "From", null, true);
			ValidateValue(builders[1].Values.First(), "To", null, true);
        }

        [Test]
		public void TwoTracesRenamed()
        {
			var tpp = new TraceParameterProvider();
			tpp.For<IEmailer>()
				.With<IEmail>()
					.Trace(email => email.From).As("Sender")
					.Trace(email => email.To).As("Recipient")
				.EndWith();

			var builders = tpp.Builders.ToArray();
			Assert.That(builders.Length, Is.EqualTo(2));

			ValidateValue(builders[0].Values.First(), "Sender", null, true);
			ValidateValue(builders[1].Values.First(), "Recipient", null, true);
		}
		#endregion

		#region And Tests
		[Test]
		public void AndTraces()
		{
			var tpp = new TraceParameterProvider();
			tpp.For<IEmailer>()
				.With<IEmail>()
					.Trace(email => email.From)
						.And(email => email.To)
				.EndWith();

			var builders = tpp.Builders.ToArray();
			Assert.That(builders.Length, Is.EqualTo(1));

			Assert.That(builders[0].Alias, Is.EqualTo("From"));

			var values = builders[0].Values.ToArray();
			ValidateValue(values[0], "From", null, true);
			ValidateValue(values[1], "To", null, true);
		}

		[Test]
		public void AndTracesAs()
		{
			var tpp = new TraceParameterProvider();
			tpp.For<IEmailer>()
				.With<IEmail>()
					.Trace(email => email.From).As("Sender")
						.And(email => email.To).As("Recipient")
				.EndWith();

			var builders = tpp.Builders.ToArray();
			Assert.That(builders.Length, Is.EqualTo(1));

			Assert.That(builders[0].Alias, Is.EqualTo("Sender"));

			var values = builders[0].Values.ToArray();
			ValidateValue(values[0], "Sender", null, true);
			ValidateValue(values[1], "Recipient", null, true);
		}

		[Test]
		public void AndTracesTogetherAs()
		{
			var tpp = new TraceParameterProvider();
			tpp.For<IEmailer>()
				.With<IEmail>()
					.Trace(email => email.From)
						.And(email => email.To)
						.As("Sender", "Recipient")
					.TogetherAs("Metadata")
				.EndWith();

			var builders = tpp.Builders.ToArray();
			Assert.That(builders.Length, Is.EqualTo(1));

			Assert.That(builders[0].Alias, Is.EqualTo("Metadata"));

			var values = builders[0].Values.ToArray();
			ValidateValue(values[0], "Sender", null, true);
			ValidateValue(values[1], "Recipient", null, true);
		}

		[Test]
		public void AndNameAlternateSyntax()
		{
			var tpp = new TraceParameterProvider();
			tpp.For<IEmailer>()
				.Trace("email", "when");

			var builders = tpp.Builders.ToArray();
			Assert.That(builders.Length, Is.EqualTo(1));
			Assert.That(builders[0].Alias, Is.EqualTo("email"));

			var values = builders[0].Values.ToArray();
			ValidateValue(values[0], "email", "email", false);
			ValidateValue(values[1], "when", "when", false);
		}

		[Test]
		public void AndExpressionAlternateSyntax()
		{
			var tpp = new TraceParameterProvider();
			tpp.For<IEmailer>()
				.With<IEmail>()
					.Trace(e => e.From, e => e.To)
						.TogetherAs("email")
				.EndWith();

			var builders = tpp.Builders.ToArray();
			Assert.That(builders.Length, Is.EqualTo(1));
			Assert.That(builders[0].Alias, Is.EqualTo("email"));

			var values = builders[0].Values.ToArray();
			ValidateValue(values[0], "From", null, true);
			ValidateValue(values[1], "To", null, true);
		}

		[Test]
		public void AndExpressionAlternateSyntaxWithRename()
		{
			var tpp = new TraceParameterProvider();
			tpp.For<IEmailer>()
				.With<IEmail>()
					.Trace(e => e.From, e => e.To).As("Sender", "Recipient")
					.TogetherAs("metadata")
				.EndWith();

			var builders = tpp.Builders.ToArray();
			Assert.That(builders.Length, Is.EqualTo(1));
			Assert.That(builders[0].Alias, Is.EqualTo("metadata"));

			var values = builders[0].Values.ToArray();
			ValidateValue(values[0], "Sender", null, true);
			ValidateValue(values[1], "Recipient", null, true);
		}
		[Test]
		public void AndExpressionWithNameAlternateSyntax()
		{
			var tpp = new TraceParameterProvider();
			tpp.For<IEmailer>()
				.With<IEmail>()
					.Trace("email", e => e.From, e => e.To)
				.EndWith();

			var builders = tpp.Builders.ToArray();
			Assert.That(builders.Length, Is.EqualTo(1));
			Assert.That(builders[0].Alias, Is.EqualTo("email"));

			var values = builders[0].Values.ToArray();
			ValidateValue(values[0], "From", "email", true);
			ValidateValue(values[1], "To", "email", true);
		}
		#endregion

		#region With Tests
		[Test]
		public void WithTypeTrace()
		{
			var tpp = new TraceParameterProvider();
			tpp.For<IEmailer>()
				.With<IEmail>()
					.Trace(e => e.From)
					.Trace(e => e.To);

			var builders = tpp.Builders.ToArray();
			Assert.That(builders.Length, Is.EqualTo(2));

			Assert.That(builders[0].Alias, Is.EqualTo("From"));
			ValidateValue(builders[0].Values.First(), "From", null, true);
			Assert.That(builders[1].Alias, Is.EqualTo("To"));
			ValidateValue(builders[1].Values.First(), "To", null, true);
		}

		[Test]
		public void WithTypeAndNameTrace()
		{
			var tpp = new TraceParameterProvider();
			tpp.For<IEmailer>()
				.With<IEmail>("email")
					.Trace(e => e.From)
					.Trace(e => e.To);

			var builders = tpp.Builders.ToArray();
			Assert.That(builders.Length, Is.EqualTo(2));

			Assert.That(builders[0].Alias, Is.EqualTo("From"));
			ValidateValue(builders[0].Values.First(), "From", "email", true);
			Assert.That(builders[1].Alias, Is.EqualTo("To"));
			ValidateValue(builders[1].Values.First(), "To", "email", true);
		}

		[Test]
		public void WithChained()
		{
			var tpp = new TraceParameterProvider();
			tpp.For<IEmailer>()
				.With<IEmail>("email")
					.Trace(e => e.From)
					.Trace(e => e.To)
				.With<OtherClass>()
					.Trace(c => c.ToString()).As("other");

			var builders = tpp.Builders.ToArray();
			Assert.That(builders.Length, Is.EqualTo(3));

			Assert.That(builders[0].Alias, Is.EqualTo("From"));
			ValidateValue(builders[0].Values.First(), "From", "email", true);
			Assert.That(builders[0].Values.First().ParameterType, Is.EqualTo(typeof(IEmail)));
			Assert.That(builders[1].Alias, Is.EqualTo("To"));
			Assert.That(builders[1].Values.First().ParameterType, Is.EqualTo(typeof(IEmail)));
			ValidateValue(builders[1].Values.First(), "To", "email", true);
			Assert.That(builders[2].Alias, Is.EqualTo("other"));
			Assert.That(builders[2].Values.First().ParameterType, Is.EqualTo(typeof(OtherClass)));
		}
		#endregion

		#region Default and Ignore Tests
		[Test]
		public void TraceAllParametersByDefault()
		{
			var tpp = new TraceParameterProvider();

			var proxy = (IEmailer)new TypeImplementer(typeof(IEmailer), tpp).EventSource;
			EnableLogging(proxy);
			proxy.Send(new EMail("from", "to", "subject", "body", new byte[][] { new byte[] { 1 } }), DateTime.Parse("1/1/2000"));

			// look at the events again
			var events = _listener.Events.ToArray();
			Assert.AreEqual(1, events.Length);

			var payload = events[0].Payload.Select(o => o.ToString()).ToArray();
			Assert.AreEqual(2, payload.Length);
			Assert.That(payload.Contains("{\"From\":\"from\",\"To\":\"to\",\"Subject\":\"subject\",\"Body\":\"body\",\"Attachments\":[\"AQ==\"]}"));
			Assert.That(payload.Contains("1/1/2000 12:00:00 AM"));
		}

		[Test]
		public void TraceOverridesDefault()
		{
			// specify how to trace an email object
			var tpp = new TraceParameterProvider();
			tpp.For<IEmailer>().With<IEmail>().Trace(e => e.From);

			var proxy = (IEmailer)new TypeImplementer(typeof(IEmailer), tpp).EventSource;
			EnableLogging(proxy);
			proxy.Send(new EMail("from", "to", "subject", "body", new byte[][] { new byte[] { 1 } }), DateTime.Parse("1/1/2000"));

			// look at the events again
			var events = _listener.Events.ToArray();
			Assert.AreEqual(1, events.Length);

			var payload = events[0].Payload.Select(o => o.ToString()).ToArray();
			Assert.AreEqual(2, payload.Length);
			Assert.That(payload.Contains("from"));
			Assert.That(payload.Contains("1/1/2000 12:00:00 AM"));
		}

		[Test]
		public void IgnoreOneParameter()
		{
			var tpp = new TraceParameterProvider();
			tpp.For<IEmailer>().Ignore("email");

			var proxy = (IEmailer)new TypeImplementer(typeof(IEmailer), tpp).EventSource;
			EnableLogging(proxy);
			proxy.Send(new EMail("from", "to", "subject", "body", new byte[][] { new byte[] { 1 } }), DateTime.Parse("1/1/2000"));

			// look at the events again
			var events = _listener.Events.ToArray();
			Assert.AreEqual(1, events.Length);

			var payload = events[0].Payload.Select(o => o.ToString()).ToArray();
			Assert.AreEqual(1, payload.Length);
			Assert.That(payload.Contains("1/1/2000 12:00:00 AM"));
		}

		[Test]
		public void IgnoreParameterByType()
		{
			var tpp = new TraceParameterProvider();
			tpp.For<IEmailer>().Ignore<IEmail>();

			var proxy = (IEmailer)new TypeImplementer(typeof(IEmailer), tpp).EventSource;
			EnableLogging(proxy);
			proxy.Send(new EMail("from", "to", "subject", "body", new byte[][] { new byte[] { 1 } }), DateTime.Parse("1/1/2000"));

			// look at the events again
			var events = _listener.Events.ToArray();
			Assert.AreEqual(1, events.Length);

			var payload = events[0].Payload.Select(o => o.ToString()).ToArray();
			Assert.AreEqual(1, payload.Length);
			Assert.That(payload.Contains("1/1/2000 12:00:00 AM"));
		}
		#endregion

		#region AddContext Tests
		[Test]
		public void AddContext()
		{
			var traceDescription = new TraceParameterProvider().For<IEmailer>()
				.AddContext("context", () => "foo");

			ParameterBuilder traceBuilder = (ParameterBuilder)traceDescription;

			Assert.That(traceBuilder.Alias, Is.EqualTo("context"));

			var traceValues = traceBuilder.Values.ToArray();
			Assert.That(traceValues.Length, Is.EqualTo(1));
			ValidateValue(traceValues[0], "context", null, true);
		}
		#endregion

		private void ValidateValue(ParameterBuilderValue value, string alias, string parameterName, bool hasExpression)
		{
			Assert.That(value.Alias, Is.EqualTo(alias));
			Assert.That(value.ParameterName, Is.EqualTo(parameterName));
			Assert.AreEqual(hasExpression, value.Converter != null);
		}
	}
}
