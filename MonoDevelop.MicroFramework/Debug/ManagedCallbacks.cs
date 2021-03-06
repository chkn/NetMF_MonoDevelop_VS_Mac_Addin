using System;
using System.Runtime.InteropServices;
using System.Diagnostics;

namespace Microsoft.SPOT.Debugger
{
	[Serializable]
	public enum LoggingLevelEnum
	{
		LTraceLevel0 = 0x0,
		LTraceLevel1 = 0x1,
		LTraceLevel2 = 0x2,
		LTraceLevel3 = 0x3,
		LTraceLevel4 = 0x4,
		LStatusLevel0 = 0x14,
		LStatusLevel1 = 0x15,
		LStatusLevel2 = 0x16,
		LStatusLevel3 = 0x17,
		LStatusLevel4 = 0x18,
		LWarningLevel = 0x28,
		LErrorLevel = 0x32,
		LPanicLevel = 0x64,
	}

	[Serializable]
	public enum CorDebugExceptionFlags
	{
		DEBUG_EXCEPTION_CAN_BE_INTERCEPTED = 1,
	}

	[Serializable]
	public enum CorDebugExceptionUnwindCallbackType
	{
		DEBUG_EXCEPTION_UNWIND_BEGIN = 1,
		DEBUG_EXCEPTION_INTERCEPTED = 2,
	}

	public interface ICorDebugManagedCallback
	{
		int Breakpoint (CorDebugAppDomain pAppDomain, CorDebugThread pThread, CorDebugBreakpointBase pBreakpoint);

		int StepComplete (CorDebugAppDomain pAppDomain, CorDebugThread pThread, CorDebugStepper pStepper, CorDebugStepReason reason);

		int Break (CorDebugAppDomain pAppDomain, CorDebugThread thread);

		int Exception (CorDebugAppDomain pAppDomain, CorDebugThread pThread, int unhandled);

		int EvalComplete (CorDebugAppDomain pAppDomain, CorDebugThread pThread, CorDebugEval pEval);

		int EvalException (CorDebugAppDomain pAppDomain, CorDebugThread pThread, CorDebugEval pEval);

		int CreateProcess (CorDebugProcess pProcess);

		int ExitProcess (CorDebugProcess pProcess);

		int CreateThread (CorDebugAppDomain pAppDomain, CorDebugThread thread);

		int ExitThread (CorDebugAppDomain pAppDomain, CorDebugThread thread);

		int LoadModule (CorDebugAppDomain pAppDomain, CorDebugAssembly pModule);

		int UnloadModule (CorDebugAppDomain pAppDomain, CorDebugAssembly pModule);

		int LoadClass (CorDebugAppDomain pAppDomain, CorDebugClass c);

		int UnloadClass (CorDebugAppDomain pAppDomain, CorDebugClass c);

		int DebuggerError (CorDebugProcess pProcess, int errorHR, uint errorCode);

		int LogMessage (CorDebugAppDomain pAppDomain, CorDebugThread pThread, int lLevel, string pLogSwitchName, string pMessage);

		int LogSwitch (CorDebugAppDomain pAppDomain, CorDebugThread pThread, int lLevel, uint ulReason, ref ushort pLogSwitchName, ref ushort pParentName);

		int CreateAppDomain (CorDebugProcess pProcess, CorDebugAppDomain pAppDomain);

		int ExitAppDomain (CorDebugProcess pProcess, CorDebugAppDomain pAppDomain);

		int LoadAssembly (CorDebugAppDomain pAppDomain, CorDebugAssembly pAssembly);

		int UnloadAssembly (CorDebugAppDomain pAppDomain, CorDebugAssembly pAssembly);

		int ControlCTrap (CorDebugProcess pProcess);

		int NameChange (CorDebugAppDomain pAppDomain, CorDebugThread pThread);
		//int UpdateModuleSymbols(CorDebugAppDomain pAppDomain, CorDebugModule pModule, Microsoft.VisualStudio.OLE.Interop.IStream pSymbolStream);
		int EditAndContinueRemap (CorDebugAppDomain pAppDomain, CorDebugThread pThread, CorDebugFunction pFunction, int fAccurate);

		int BreakpointSetError (CorDebugAppDomain pAppDomain, CorDebugThread pThread, CorDebugBreakpoint pBreakpoint, uint dwError);
		//ICorDebugManagedCallback2
		void ExceptionUnwind (CorDebugAppDomain appDomain, CorDebugThread m_thread, CorDebugExceptionUnwindCallbackType m_type, int i);

		int Exception (CorDebugAppDomain pAppDomain, CorDebugThread pThread, CorDebugFrame pFrame, uint nOffset, CorDebugExceptionCallbackType dwEventType, uint dwFlags);
	}

	public class ManagedCallbacks
	{
		public abstract class ManagedCallback
		{
			public abstract void Dispatch (ICorDebugManagedCallback callback);
		}

		public class ManagedCallbackThread : ManagedCallback
		{
			public enum EventType
			{
				CreateThread,
				ExitThread,
				NameChange,
				Other,
			}

			protected CorDebugThread m_thread;
			private EventType m_eventType;
			protected bool m_fSuspendThreadEvents;

			public ManagedCallbackThread (CorDebugThread thread, EventType eventType)
			{
				//breakpoints can happen on virtual threads..
				m_thread = thread.GetRealCorDebugThread ();
				m_eventType = eventType;
				m_fSuspendThreadEvents = (m_eventType == EventType.CreateThread);
			}

			public ManagedCallbackThread (CorDebugThread thread) : this (thread, EventType.Other)
			{
			}

			public CorDebugThread Thread {
				get { return m_thread.GetRealCorDebugThread (); }
			}

			public sealed override void Dispatch (ICorDebugManagedCallback callback)
			{
				bool fSuspendThreadEventsSav = m_thread.SuspendThreadEvents;

				try {
					m_thread.SuspendThreadEvents = m_fSuspendThreadEvents;
					DispatchThreadEvent (callback);
				} finally {
					m_thread.SuspendThreadEvents = fSuspendThreadEventsSav;
				}
			}

			public virtual void DispatchThreadEvent (ICorDebugManagedCallback callback)
			{
				switch (m_eventType) {
				case EventType.CreateThread:
					callback.CreateThread (m_thread.AppDomain, m_thread);
					break;

				case EventType.ExitThread:
					callback.ExitThread (m_thread.AppDomain, m_thread);
					break;

				case EventType.NameChange:
					callback.NameChange (m_thread.AppDomain, m_thread);
					break;

				case EventType.Other:
					Debug.Assert (false, "Invalid ManagedCallbackThread event");
					break;
				}
			}
		}

		public class ManagedCallbackBreakpoint : ManagedCallbackThread
		{
			protected CorDebugBreakpoint m_breakpoint;
			protected Type m_typeToMarshal;

			public ManagedCallbackBreakpoint (CorDebugThread thread, CorDebugBreakpoint breakpoint, Type typeToMarshal) : base (thread)
			{
				m_breakpoint = breakpoint;
				m_typeToMarshal = typeToMarshal;
			}

			public ManagedCallbackBreakpoint (CorDebugThread thread, CorDebugBreakpoint breakpoint) : this (thread, breakpoint, typeof(CorDebugBreakpoint))
			{
			}

			public override void DispatchThreadEvent (ICorDebugManagedCallback callback)
			{
				callback.Breakpoint (m_thread.AppDomain, m_thread, m_breakpoint);
			}
		}

		public class ManagedCallbackDebugMessage : ManagedCallbackThread
		{
			private string m_switchName;
			private string m_message;
			private LoggingLevelEnum m_level;
			private CorDebugAppDomain m_appDomain;

			public ManagedCallbackDebugMessage (CorDebugThread thread, CorDebugAppDomain appDomain, string switchName, string message, LoggingLevelEnum level) : base (thread)
			{
				m_switchName = switchName;
				m_message = message;
				m_level = level;
				m_appDomain = appDomain;
			}

			public override void DispatchThreadEvent (ICorDebugManagedCallback callback)
			{
				callback.LogMessage (m_appDomain, m_thread, (int)m_level, m_switchName, m_message);
			}
		}

		public class ManagedCallbackBreakpointSetError : ManagedCallbackBreakpoint
		{
			uint m_error;

			public ManagedCallbackBreakpointSetError (CorDebugThread thread, CorDebugBreakpoint breakpoint, uint error) : base (thread, breakpoint)
			{
				m_error = error;
			}

			public override void DispatchThreadEvent (ICorDebugManagedCallback callback)
			{
				callback.BreakpointSetError (m_thread.AppDomain, m_thread, m_breakpoint, m_error);
			}
		}

		public class ManagedCallbackStepComplete : ManagedCallbackThread
		{
			CorDebugStepper m_stepper;
			CorDebugStepReason m_reason;

			public ManagedCallbackStepComplete (CorDebugThread thread, CorDebugStepper stepper, CorDebugStepReason reason) : base (thread)
			{
				m_stepper = stepper;
				m_reason = reason;
			}

			public override void DispatchThreadEvent (ICorDebugManagedCallback callback)
			{
				callback.StepComplete (m_thread.AppDomain, m_thread, m_stepper, m_reason);
			}
		}

		public class ManagedCallbackBreak : ManagedCallbackThread
		{
			public ManagedCallbackBreak (CorDebugThread thread) : base (thread)
			{
			}

			public override void DispatchThreadEvent (ICorDebugManagedCallback callback)
			{
				callback.Break (m_thread.AppDomain, m_thread);
			}
		}

		public class ManagedCallbackException : ManagedCallbackThread
		{
			CorDebugExceptionCallbackType m_type;
			CorDebugFrame m_frame;
			uint m_ip;

			public ManagedCallbackException (CorDebugThread thread, CorDebugFrame frame, uint ip, CorDebugExceptionCallbackType type) : base (thread)
			{
				m_type = type;
				m_frame = frame;
				m_ip = ip;

				if (!thread.Engine.Capabilities.ExceptionFilters) {
					m_fSuspendThreadEvents = (m_type == CorDebugExceptionCallbackType.DEBUG_EXCEPTION_FIRST_CHANCE) || (m_type == CorDebugExceptionCallbackType.DEBUG_EXCEPTION_USER_FIRST_CHANCE);
				}

				//Because we are now doing two-pass exception handling, the stack's IP isn't going to be the handler, so
				//we have to use the IP sent via the breakpointDef and not the stack frame.
				if (m_frame != null && m_frame.Function != null && m_frame.Function.HasSymbols) {
					m_ip = m_frame.Function.GetILCLRFromILTinyCLR (m_ip);
				}
			}

			public override void DispatchThreadEvent (ICorDebugManagedCallback callback)
			{
				callback.Exception (m_thread.AppDomain, m_thread, m_frame, m_ip, m_type, (uint)CorDebugExceptionFlags.DEBUG_EXCEPTION_CAN_BE_INTERCEPTED);
			}
		}

		public class ManagedCallbackExceptionUnwind : ManagedCallbackThread
		{
			CorDebugExceptionUnwindCallbackType m_type;
			CorDebugFrame m_frame;

			public ManagedCallbackExceptionUnwind (CorDebugThread thread, CorDebugFrame frame, CorDebugExceptionUnwindCallbackType type) : base (thread)
			{
				Debug.Assert (type == CorDebugExceptionUnwindCallbackType.DEBUG_EXCEPTION_INTERCEPTED, "UnwindBegin is not supported");
				m_type = type;
				m_frame = frame;
			}

			public override void DispatchThreadEvent (ICorDebugManagedCallback callback)
			{
				callback.ExceptionUnwind (m_thread.AppDomain, m_thread, m_type, 0);
			}
		}

		public class ManagedCallbackEval : ManagedCallbackThread
		{
			public new enum EventType
			{
				EvalComplete,
				EvalException
			}

			CorDebugEval m_eval;
			EventType m_eventType;

			public ManagedCallbackEval (CorDebugThread thread, CorDebugEval eval, EventType eventType) : base (thread)
			{
				m_eval = eval;
				m_eventType = eventType;
			}

			public override void DispatchThreadEvent (ICorDebugManagedCallback callback)
			{
				switch (m_eventType) {
				case EventType.EvalComplete:
					callback.EvalComplete (m_thread.AppDomain, m_thread, m_eval);
					break;

				case EventType.EvalException:
					callback.EvalException (m_thread.AppDomain, m_thread, m_eval);
					break;
				}
			}
		}

		public class ManagedCallbackProcess : ManagedCallback
		{
			public enum EventType
			{
				CreateProcess,
				ExitProcess,
				ControlCTrap,
				Other
			}

			protected CorDebugProcess m_process;
			EventType m_eventType;

			public ManagedCallbackProcess (CorDebugProcess process, EventType eventType)
			{
				m_process = process;
				m_eventType = eventType;
			}

			public override void Dispatch (ICorDebugManagedCallback callback)
			{
				switch (m_eventType) {
				case EventType.CreateProcess:
					callback.CreateProcess (m_process);
					break;
				case EventType.ExitProcess:
					callback.ExitProcess (m_process);
					break;
				case EventType.ControlCTrap:
					callback.ControlCTrap (m_process);
					break;
				case EventType.Other:
					Debug.Assert (false, "Invalid ManagedCallbackProcess event");
					break;
				}
			}
		}

		public class ManagedCallbackProcessError : ManagedCallbackProcess
		{
			int m_errorHR;
			uint m_errorCode;

			public ManagedCallbackProcessError (CorDebugProcess process, int errorHR, uint errorCode) : base (process, EventType.Other)
			{
				m_errorHR = errorHR;
				m_errorCode = errorCode;
			}

			public override void Dispatch (ICorDebugManagedCallback callback)
			{
				callback.DebuggerError (m_process, m_errorHR, m_errorCode);
			}
		}

		public class ManagedCallbackAppDomain : ManagedCallback
		{
			public enum EventType
			{
				CreateAppDomain,
				ExitAppDomain,
				Other,
			}

			protected CorDebugAppDomain m_appDomain;
			EventType m_eventType;

			public ManagedCallbackAppDomain (CorDebugAppDomain appDomain, EventType eventType)
			{
				m_appDomain = appDomain;
				m_eventType = eventType;
			}

			public override void Dispatch (ICorDebugManagedCallback callback)
			{
				switch (m_eventType) {
				case EventType.CreateAppDomain:
					callback.CreateAppDomain (m_appDomain.Process, m_appDomain);
					break;
				case EventType.ExitAppDomain:
					callback.ExitAppDomain (m_appDomain.Process, m_appDomain);
					break;
				case EventType.Other:
					Debug.Assert (false, "Invalid ManagedCallbackAppDomain event");
					break;
				}
			}
		}

		public class ManagedCallbackAssembly : ManagedCallback
		{
			public enum EventType
			{
				LoadAssembly,
				LoadModule,
				UnloadAssembly,
				UnloadModule
			}

			CorDebugAssembly m_assembly;
			EventType m_eventType;

			public ManagedCallbackAssembly (CorDebugAssembly assembly, EventType eventType)
			{
				m_assembly = assembly;
				m_eventType = eventType;
			}

			public override void Dispatch (ICorDebugManagedCallback callback)
			{
				switch (m_eventType) {
				case EventType.LoadAssembly:
					callback.LoadAssembly (m_assembly.AppDomain, m_assembly);
					break;

				case EventType.LoadModule:
					callback.LoadModule (m_assembly.AppDomain, m_assembly);
					break;

				case EventType.UnloadAssembly:
					callback.UnloadAssembly (m_assembly.AppDomain, m_assembly);
					break;

				case EventType.UnloadModule:
					callback.UnloadModule (m_assembly.AppDomain, m_assembly);
					break;
				}
			}
		}

		public class ManagedCallbackClass : ManagedCallback
		{
			public enum EventType
			{
				LoadClass,
				UnloadClass
			}

			CorDebugClass m_class;
			EventType m_eventType;

			public ManagedCallbackClass (CorDebugClass c, EventType eventType)
			{
				m_class = c;
				m_eventType = eventType;
			}

			public override void Dispatch (ICorDebugManagedCallback callback)
			{
				CorDebugAppDomain appDomain = m_class.Assembly.AppDomain;
				switch (m_eventType) {
				case EventType.LoadClass:
					callback.LoadClass (appDomain, m_class);
					break;

				case EventType.UnloadClass:
					callback.UnloadClass (appDomain, m_class);
					break;
				}
			}
		}
	}
}
