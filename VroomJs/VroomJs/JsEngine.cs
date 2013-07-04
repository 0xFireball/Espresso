// This file is part of the VroomJs library.
//
// Author:
//     Federico Di Gregorio <fog@initd.org>
//
// Copyright © 2013 Federico Di Gregorio <fog@initd.org>
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace VroomJs
{
	public class JsEngine : IDisposable
	{
        delegate void KeepaliveRemoveDelegate(int slot);
        delegate JsValue KeepAliveGetPropertyValueDelegate(int slot, [MarshalAs(UnmanagedType.LPWStr)] string name);
        delegate JsValue KeepAliveSetPropertyValueDelegate(int slot, [MarshalAs(UnmanagedType.LPWStr)] string name, JsValue value);
        delegate JsValue KeepAliveInvokeDelegate(int slot, JsValue args);

		[DllImport("VroomJsNative")]
        static extern IntPtr jsengine_new(
            KeepaliveRemoveDelegate keepaliveRemove,
            KeepAliveGetPropertyValueDelegate keepaliveGetPropertyValue,
            KeepAliveSetPropertyValueDelegate keepaliveSetPropertyValue,
            KeepAliveInvokeDelegate keepaliveInvoke
        );

		[DllImport("VroomJsNative")]
        static extern void jsengine_set_object_marshal_type(JsObjectMarshalType objectMarshalType);
		
		[DllImport("VroomJsNative")]
        static extern void jsengine_dispose(HandleRef engine);

		[DllImport("VroomJsNative")]
        static extern void jsengine_force_gc();

		[DllImport("VroomJsNative")]
        static extern void jsengine_dispose_object(HandleRef engine, IntPtr obj);

		[DllImport("VroomJsNative")]
        static extern JsValue jsengine_execute(HandleRef engine, [MarshalAs(UnmanagedType.LPWStr)] string str);

		[DllImport("VroomJsNative")]
		static extern JsValue jsengine_get_global(HandleRef engine);

		[DllImport("VroomJsNative")]
        static extern JsValue jsengine_get_variable(HandleRef engine, [MarshalAs(UnmanagedType.LPWStr)] string name);

		[DllImport("VroomJsNative")]
        static extern JsValue jsengine_set_variable(HandleRef engine, [MarshalAs(UnmanagedType.LPWStr)] string name, JsValue value);

		[DllImport("VroomJsNative")]
		static extern JsValue jsengine_get_property_names(HandleRef engine, IntPtr ptr);

		[DllImport("VroomJsNative")]
        static extern JsValue jsengine_get_property_value(HandleRef engine, IntPtr ptr, [MarshalAs(UnmanagedType.LPWStr)] string name);

		[DllImport("VroomJsNative")]
        static extern JsValue jsengine_set_property_value(HandleRef engine, IntPtr ptr, [MarshalAs(UnmanagedType.LPWStr)] string name, JsValue value);

		[DllImport("VroomJsNative")]
        static extern JsValue jsengine_invoke_property(HandleRef engine, IntPtr ptr, [MarshalAs(UnmanagedType.LPWStr)] string name, JsValue args);

		[DllImport("VroomJsNative")]
        static internal extern JsValue jsvalue_alloc_string([MarshalAs(UnmanagedType.LPWStr)] string str);

		[DllImport("VroomJsNative")]
        static internal extern JsValue jsvalue_alloc_array(int length);

		[DllImport("VroomJsNative")]
        static internal extern void jsvalue_dispose(JsValue value);

		static JsEngine() {
			JsObjectMarshalType objectMarshalType = JsObjectMarshalType.Dictionary;
#if NET40
        	objectMarshalType = JsObjectMarshalType.Dynamic;
#endif
			jsengine_set_object_marshal_type(objectMarshalType);
		}

        public JsEngine() {
            _keepalives = new KeepAliveDictionaryStore();
			_keepalive_remove = new KeepaliveRemoveDelegate(KeepAliveRemove);
            _keepalive_get_property_value = new KeepAliveGetPropertyValueDelegate(KeepAliveGetPropertyValue);
            _keepalive_set_property_value = new KeepAliveSetPropertyValueDelegate(KeepAliveSetPropertyValue);
            _keepalive_invoke = new KeepAliveInvokeDelegate(KeepAliveInvoke);

            _engine = new HandleRef(this, jsengine_new(
                _keepalive_remove, 
                _keepalive_get_property_value, _keepalive_set_property_value,
                _keepalive_invoke));

            _convert = new JsConvert(this);
		}

        readonly HandleRef _engine;
        readonly JsConvert _convert;

        // Keep objects passed to V8 alive even if no other references exist.
        readonly IKeepAliveStore _keepalives;

        // Make sure the delegates we pass to the C++ engine won't fly away during a GC.
        readonly KeepaliveRemoveDelegate _keepalive_remove;
        readonly KeepAliveGetPropertyValueDelegate _keepalive_get_property_value;
        readonly KeepAliveSetPropertyValueDelegate _keepalive_set_property_value;
        readonly KeepAliveInvokeDelegate _keepalive_invoke;

        public JsEngineStats GetStats()
        {
            return new JsEngineStats {
                KeepAliveMaxSlots = _keepalives.MaxSlots,
                KeepAliveAllocatedSlots = _keepalives.AllocatedSlots,
                KeepAliveUsedSlots = _keepalives.UsedSlots
            };
        }

        public object Execute(string code)
        {
            if (code == null)
                throw new ArgumentNullException("code");

            CheckDisposed();

            JsValue v = jsengine_execute(_engine, code);
            object res = _convert.FromJsValue(v);
            jsvalue_dispose(v);

            Exception e = res as JsException;
            if (e != null)
                throw e;
            return res;
        }

		public object GetGlobal() 
		{
			CheckDisposed();	
			JsValue v = jsengine_get_global(_engine);
            object res = _convert.FromJsValue(v);
            jsvalue_dispose(v);

            Exception e = res as JsException;
            if (e != null)
                throw e;
            return res;
		}

        public object GetVariable(string name)
        {
            if (name == null)
                throw new ArgumentNullException("name");

            CheckDisposed();

            JsValue v = jsengine_get_variable(_engine, name);
            object res = _convert.FromJsValue(v);
            jsvalue_dispose(v);

            Exception e = res as JsException;
            if (e != null)
                throw e;
            return res;
        }

        public void SetVariable(string name, object value)
        {
            if (name == null)
                throw new ArgumentNullException("name");

            CheckDisposed();

            JsValue a = _convert.ToJsValue(value);
            jsengine_set_variable(_engine, name, a);
            jsvalue_dispose(a);

            // TODO: Check the result of the operation for errors.
        }

#if NET40 
		public IEnumerable<string> GetMemberNames(JsObject obj) {
			if (obj == null)
				throw new ArgumentNullException("obj");

			CheckDisposed();

			if (obj.Handle == IntPtr.Zero)
				throw new JsInteropException("wrapped V8 object is empty (IntPtr is Zero)");

			JsValue v = jsengine_get_property_names(_engine, obj.Handle);
			object res = _convert.FromJsValue(v);
			jsvalue_dispose(v);

			Exception e = res as JsException;
			if (e != null)
				throw e;

			object[] arr = (object[])res;
			return arr.Cast<string>();
		}


        public object GetPropertyValue(JsObject obj, string name)
        {
            if (obj == null)
                throw new ArgumentNullException("obj");
            if (name == null)
                throw new ArgumentNullException("name");

            CheckDisposed();

            if (obj.Handle == IntPtr.Zero)
                throw new JsInteropException("wrapped V8 object is empty (IntPtr is Zero)");

            JsValue v = jsengine_get_property_value(_engine, obj.Handle, name);
            object res = _convert.FromJsValue(v);
            jsvalue_dispose(v);

            Exception e = res as JsException;
            if (e != null)
                throw e;
            return res;
        }

        public void SetPropertyValue(JsObject obj, string name, object value)
        {
            if (obj == null)
                throw new ArgumentNullException("obj");
            if (name == null)
                throw new ArgumentNullException("name");

            CheckDisposed();

            if (obj.Handle == IntPtr.Zero)
                throw new JsInteropException("wrapped V8 object is empty (IntPtr is Zero)");

            JsValue a = _convert.ToJsValue(value);
            JsValue v = jsengine_set_property_value(_engine, obj.Handle, name, a);
            object res = _convert.FromJsValue(v);
            jsvalue_dispose(v);
            jsvalue_dispose(a);

            Exception e = res as JsException;
            if (e != null)
                throw e;
        }

        public object InvokeProperty(JsObject obj, string name, object[] args)
        {
            if (obj == null)
                throw new ArgumentNullException("obj");
            if (name == null)
                throw new ArgumentNullException("name");

            CheckDisposed();

            if (obj.Handle == IntPtr.Zero)
                throw new JsInteropException("wrapped V8 object is empty (IntPtr is Zero)");

            JsValue a = JsValue.Null; // Null value unless we're given args.
            if (args != null)
                a = _convert.ToJsValue(args);

            JsValue v = jsengine_invoke_property(_engine, obj.Handle, name, a);
            object res = _convert.FromJsValue(v);
            jsvalue_dispose(v);
            jsvalue_dispose(a);

            Exception e = res as JsException;
            if (e != null)
                throw e;
            return res;
        }

        public void DisposeObject(JsObject obj)
        {
            // If the engine has already been explicitly disposed we pass Zero as
            // the first argument because we need to free the memory allocated by
            // "new" but not the object on the V8 heap: it has already been freed.
            if (_disposed)
                jsengine_dispose_object(new HandleRef(this, IntPtr.Zero), obj.Handle);
            else
                jsengine_dispose_object(_engine, obj.Handle);
        }
#endif
		public void Flush()
        {
            jsengine_force_gc();
        }

        #region Keep-alive management and callbacks.

        internal int KeepAliveAdd(object obj)
        {
            return _keepalives.Add(obj);
        }

        internal object KeepAliveGet(int slot)
        {
            return _keepalives.Get(slot);
        }

        internal void KeepAliveRemove(int slot)
        {
            _keepalives.Remove(slot);
        }

        JsValue KeepAliveGetPropertyValue(int slot, [MarshalAs(UnmanagedType.LPWStr)] string name)
        {
            // TODO: This is pretty slow: use a cache of generated code to make it faster.

            var obj = KeepAliveGet(slot);
            if (obj != null) {
                Type type = obj.GetType();

                try {
                    // First of all try with a public property (the most common case).

                    PropertyInfo pi = type.GetProperty(name, BindingFlags.Instance|BindingFlags.Public|BindingFlags.GetProperty);
                    if (pi != null)
                        return _convert.ToJsValue(pi.GetValue(obj, null));

                    // Then with an instance method: the problem is that we don't have a list of
                    // parameter types so we just check if any method with the given name exists
                    // and then keep alive a "weak delegate", i.e., just a name and the target.
                    // The real method will be resolved during the invokation itself.

                    const BindingFlags mFlags = BindingFlags.Instance|BindingFlags.Public
                                               |BindingFlags.InvokeMethod|BindingFlags.FlattenHierarchy;
                    // TODO: This is probably slooow.
                    if (type.GetMethods(mFlags).Any(x => x.Name == name))
                        return _convert.ToJsValue(new WeakDelegate(obj, name));

                    // Else an error.

                    return JsValue.Error(KeepAliveAdd(
                        new InvalidOperationException(String.Format("property not found on {0}: {1} ", type, name)))); 
                }
                catch (TargetInvocationException e) {
                    // Client code probably isn't interested in the exception part related to
                    // reflection, so we unwrap it and pass to V8 only the real exception thrown.
                    if (e.InnerException != null)
                        return JsValue.Error(KeepAliveAdd(e.InnerException));
                    throw;
                }
                catch (Exception e) {
                    return JsValue.Error(KeepAliveAdd(e));
                }
            }

            return JsValue.Error(KeepAliveAdd(new IndexOutOfRangeException("invalid keepalive slot: " + slot))); 
        }

        JsValue KeepAliveSetPropertyValue(int slot, [MarshalAs(UnmanagedType.LPWStr)] string name, JsValue value)
        {
            // TODO: This is pretty slow: use a cache of generated code to make it faster.

            var obj = KeepAliveGet(slot);
            if (obj != null) {
                Type type = obj.GetType();

                // We can only set properties; everything else is an error.
                try {
                    PropertyInfo pi = type.GetProperty(name, BindingFlags.Instance|BindingFlags.Public|BindingFlags.SetProperty);
                    if (pi != null) {
                        pi.SetValue(obj, _convert.FromJsValue(value), null);
                        return JsValue.Null;
                    }

                    return JsValue.Error(KeepAliveAdd(
                        new InvalidOperationException(String.Format("property not found on {0}: {1} ", type, name)))); 
                }
                catch (Exception e) {
                    return JsValue.Error(KeepAliveAdd(e));
                }
            }

            return JsValue.Error(KeepAliveAdd(new IndexOutOfRangeException("invalid keepalive slot: " + slot))); 
        }

        JsValue KeepAliveInvoke(int slot, JsValue args)
        {
            // TODO: This is pretty slow: use a cache of generated code to make it faster.

            Console.WriteLine(args);

            var obj = KeepAliveGet(slot) as WeakDelegate;
            if (obj != null) {
                Type type = obj.Target.GetType();
                object[] a = (object[])_convert.FromJsValue(args);

                try {
                    const BindingFlags flags = BindingFlags.Instance|BindingFlags.Public
                        |BindingFlags.InvokeMethod|BindingFlags.FlattenHierarchy;
                    return _convert.ToJsValue(type.InvokeMember(obj.MethodName, flags, null, obj.Target, a));
                }
                catch (Exception e) {
                    return JsValue.Error(KeepAliveAdd(e));
                }
            }

            return JsValue.Error(KeepAliveAdd(new IndexOutOfRangeException("invalid keepalive slot: " + slot))); 
        }

        #endregion

        #region IDisposable implementation

        bool _disposed;

        public bool IsDisposed {
            get { return _disposed; }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            CheckDisposed();

            _disposed = true;

            if (disposing) {
                _keepalives.Clear();
            }

            jsengine_dispose(_engine);
        }

        void CheckDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException("JsEngine:" + _engine.Handle);
        }

        ~JsEngine()
        {
            if (!_disposed)
                Dispose(false);
        }

        #endregion

		
	}
}
