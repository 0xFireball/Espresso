// This file is part of the VroomJs library.
//
// Author:
//     Federico Di Gregorio <fog@initd.org>
//
// Copyright (c) 2013 
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

#ifndef LIBVROOMJS_H
#define LIBVROOMJS_H 1

#include <v8.h>
#include <stdlib.h>
#include <stdint.h>

using namespace v8;

// jsvalue (JsValue on the CLR side) is a struct that can be easily marshaled
// by simply blitting its value (being only 16 bytes should be quite fast too).

#define JSVALUE_TYPE_UNKNOWN_ERROR  -1
#define JSVALUE_TYPE_NULL            0
#define JSVALUE_TYPE_BOOLEAN         1
#define JSVALUE_TYPE_INTEGER         2
#define JSVALUE_TYPE_NUMBER          3
#define JSVALUE_TYPE_STRING          4
#define JSVALUE_TYPE_DATE            5
#define JSVALUE_TYPE_INDEX           6
#define JSVALUE_TYPE_ARRAY          10
#define JSVALUE_TYPE_ERROR          11
#define JSVALUE_TYPE_MANAGED        12
#define JSVALUE_TYPE_MANAGED_ERROR  13
#define JSVALUE_TYPE_WRAPPED        14
#define JSVALUE_TYPE_WRAPPED_ERROR  15

#ifdef _WIN32 
#define EXPORT __declspec(dllexport)
#elif 
#define EXPORT
#endif

extern "C" 
{
    struct jsvalue
    {
        // 8 bytes is the maximum CLR alignment; by putting the union first and a
        // int64_t inside it we make (almost) sure the offset of 'type' will always
        // be 8 and the total size 16. We add a check to jsengine_new anyway.
        
        union 
        {
            int32_t     i32;
            int64_t     i64;
            double      num;
            void       *ptr;
            uint16_t   *str;
            jsvalue    *arr;
        } value;
        
        int32_t         type;
        int32_t         length; // Also used as slot index on the CLR side.
    };
    
   EXPORT void jsvalue_dispose(jsvalue value);
}

// The only way for the C++/V8 side to call into the CLR is to use the function
// pointers (CLR, delegates) defined below.

extern "C" 
{
    // We don't have a keepalive_add_f because that is managed on the managed side.
    // Its definition would be "int (*keepalive_add_f) (ManagedRef obj)".
    
    typedef void (*keepalive_remove_f) (int id);
    typedef jsvalue (*keepalive_get_property_value_f) (int id, uint16_t* name);
    typedef jsvalue (*keepalive_set_property_value_f) (int id, uint16_t* name, jsvalue value);
    typedef jsvalue (*keepalive_invoke_f) (int id, jsvalue args);
}

// JsEngine is a single isolated v8 interpreter and is the referenced as an IntPtr
// by the JsEngine on the CLR side.

class JsEngine {
 public:
    static JsEngine* New();
 
    inline void SetRemoveDelegate(keepalive_remove_f delegate) { keepalive_remove_ = delegate; }
    inline void SetGetPropertyValueDelegate(keepalive_get_property_value_f delegate) { keepalive_get_property_value_ = delegate; }
    inline void SetSetPropertyValueDelegate(keepalive_set_property_value_f delegate) { keepalive_set_property_value_ = delegate; }
    inline void SetInvokeDelegate(keepalive_invoke_f delegate) { keepalive_invoke_ = delegate; }
    
    // Call delegates into managed code.
    inline void CallRemove(int id) { keepalive_remove_(id); }
    inline jsvalue CallGetPropertyValue(int32_t id, uint16_t* name) { return keepalive_get_property_value_(id, name); }
    inline jsvalue CallSetPropertyValue(int32_t id, uint16_t* name, jsvalue value) { return keepalive_set_property_value_(id, name, value); }
    inline jsvalue CallInvoke(int32_t id, jsvalue args) { return keepalive_invoke_(id, args); }
    
    // Called by bridge to execute JS from managed code.
    jsvalue Execute(const uint16_t* str);    
	jsvalue GetGlobal();
    jsvalue GetVariable(const uint16_t* name);
    jsvalue SetVariable(const uint16_t* name, jsvalue value);
	jsvalue GetPropertyNames(Persistent<Object>* obj);
    jsvalue GetPropertyValue(Persistent<Object>* obj, const uint16_t* name);
    jsvalue SetPropertyValue(Persistent<Object>* obj, const uint16_t* name, jsvalue value);
    jsvalue InvokeProperty(Persistent<Object>* obj, const uint16_t* name, jsvalue args);
    
    // Conversions. Note that all the conversion functions should be called
    // with an HandleScope already on the stack or sill misarabily fail.
    Handle<Value> AnyToV8(jsvalue value); 
    jsvalue ErrorFromV8(TryCatch& trycatch);
    jsvalue StringFromV8(Handle<Value> value);
    jsvalue WrappedFromV8(Handle<Object> obj);
    jsvalue ManagedFromV8(Handle<Object> obj);
    jsvalue AnyFromV8(Handle<Value> value);
    
    // Needed to create an array of args on the stack for calling functions.
    int32_t ArrayToV8Args(jsvalue value, Handle<Value> preallocatedArgs[]);     
    
    // Converts JS function Arguments to an array of jsvalue to call managed code.
    jsvalue ArrayFromArguments(const Arguments& args);
    
    // Dispose a Persistent<Object> that was pinned on the CLR side by JsObject.
    void DisposeObject(Persistent<Object>* obj);
    
    void Dispose();
                
 private:             
    inline JsEngine() {}
   
    Isolate *isolate_;
    Persistent<Context> *context_;
    Persistent<ObjectTemplate> *managed_template_;
    keepalive_remove_f keepalive_remove_;
    keepalive_get_property_value_f keepalive_get_property_value_;
    keepalive_set_property_value_f keepalive_set_property_value_;
    keepalive_invoke_f keepalive_invoke_;
};

class ManagedRef {
 public:
    inline explicit ManagedRef(JsEngine* engine, int id) : engine_(engine), id_(id) {}
    
    inline int32_t Id() { return id_; }
    
    Handle<Value> GetPropertyValue(Local<String> name);
    Handle<Value> SetPropertyValue(Local<String> name, Local<Value> value);
    Handle<Value> Invoke(const Arguments& args);
    
    ~ManagedRef() { engine_->CallRemove(id_); }
    
 private:
    ManagedRef() {}
    JsEngine* engine_;
    int32_t id_;
};

#endif