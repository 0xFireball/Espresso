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

#include "vroomjs.h"

using namespace v8;

int js_object_marshal_type;

extern "C" 
{
	EXPORT void jsengine_set_object_marshal_type(int32_t type)
    {
        js_object_marshal_type = type;
    }

    EXPORT JsEngine* jsengine_new(keepalive_remove_f keepalive_remove, 
                           keepalive_get_property_value_f keepalive_get_property_value,
                           keepalive_set_property_value_f keepalive_set_property_value,
                           keepalive_invoke_f keepalive_invoke)
    {
        JsEngine* engine = JsEngine::New();
        if (engine != NULL) {
            engine->SetRemoveDelegate(keepalive_remove);
            engine->SetGetPropertyValueDelegate(keepalive_get_property_value);
            engine->SetSetPropertyValueDelegate(keepalive_set_property_value);
            engine->SetInvokeDelegate(keepalive_invoke);
        }
        return engine;
    }

   EXPORT void jsengine_dispose(JsEngine* engine)
    {
        engine->Dispose();        
        delete engine;
    }
    
    EXPORT void jsengine_dispose_object(JsEngine* engine, Persistent<Object>* obj)
    {
        if (engine != NULL)
            engine->DisposeObject(obj);
        delete obj;
    }     
    
    EXPORT void jsengine_force_gc()
    {
        while(!V8::IdleNotification()) {};
    }
    
    EXPORT jsvalue jsengine_execute(JsEngine* engine, const uint16_t* str)
    {
        return engine->Execute(str);
    }
        
	EXPORT jsvalue jsengine_get_global(JsEngine* engine)
    {
        return engine->GetGlobal();
    }
	
    EXPORT jsvalue jsengine_set_variable(JsEngine* engine, const uint16_t* name, jsvalue value)
    {
        return engine->SetVariable(name, value);
    }

    EXPORT jsvalue jsengine_get_variable(JsEngine* engine, const uint16_t* name)
    {
        return engine->GetVariable(name);
    }

    EXPORT jsvalue jsengine_get_property_value(JsEngine* engine, Persistent<Object>* obj, const uint16_t* name)
    {
        return engine->GetPropertyValue(obj, name);
    }
    
    EXPORT jsvalue jsengine_set_property_value(JsEngine* engine, Persistent<Object>* obj, const uint16_t* name, jsvalue value)
    {
        return engine->SetPropertyValue(obj, name, value);
    }    

	EXPORT jsvalue jsengine_get_property_names(JsEngine* engine, Persistent<Object>* obj)
    {
        return engine->GetPropertyNames(obj);
    }    
	    
    EXPORT jsvalue jsengine_invoke_property(JsEngine* engine, Persistent<Object>* obj, const uint16_t* name, jsvalue args)
    {
        return engine->InvokeProperty(obj, name, args);
    }        

    EXPORT jsvalue jsvalue_alloc_string(const uint16_t* str)
    {
        jsvalue v;
    
        int length = 0;
        while (str[length] != '\0')
            length++;
          
        v.length = length;
        v.value.str = new uint16_t[length+1];
        if (v.value.str != NULL) {
            for (int i=0 ; i < length ; i++)
                 v.value.str[i] = str[i];
            v.value.str[length] = '\0';
            v.type = JSVALUE_TYPE_STRING;
        }

        return v;
    }    
    
    EXPORT jsvalue jsvalue_alloc_array(const int32_t length)
    {
        jsvalue v;
          
        v.value.arr = new jsvalue[length];
        if (v.value.arr != NULL) {
            v.length = length;
            v.type = JSVALUE_TYPE_ARRAY;
        }

        return v;
    }        
                
    EXPORT void jsvalue_dispose(jsvalue value)
    {
        if (value.type == JSVALUE_TYPE_STRING || value.type == JSVALUE_TYPE_ERROR) {
            if (value.value.str != NULL)
                delete value.value.str;
        }
        else if (value.type == JSVALUE_TYPE_ARRAY) {
            for (int i=0 ; i < value.length ; i++)
                jsvalue_dispose(value.value.arr[i]);
            if (value.value.arr != NULL)
                delete value.value.arr;
        }            
    }       
}
