﻿#region License
// Copyright 2010 Buu Nguyen, Morten Mertner
// 
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
// 
// http://www.apache.org/licenses/LICENSE-2.0 
// 
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
// 
// The latest version of this file can be found at http://fasterflect.codeplex.com/
#endregion

using System;
using System.Reflection;
using System.Reflection.Emit;

namespace Fasterflect.Emitter
{
	internal class MethodInvocationEmitter : InvocationEmitter
	{
		public MethodInvocationEmitter( MethodInfo methodInfo, Flags bindingFlags )
			: this( methodInfo.DeclaringType, bindingFlags, methodInfo.Name, methodInfo.GetParameters().ToTypeArray(), methodInfo )
		{
		}

		public MethodInvocationEmitter( Type targetType, Flags bindingFlags, string name, Type[] parameterTypes )
			: this( targetType, bindingFlags, name, parameterTypes, null )
		{
		}

		private MethodInvocationEmitter( Type targetType, Flags bindingFlags, string name, Type[] parameterTypes,
		                                 MemberInfo methodInfo )
            : base(new CallInfo(targetType, bindingFlags, MemberTypes.Method, name, parameterTypes, methodInfo))
		{
		}

		protected internal override DynamicMethod CreateDynamicMethod()
		{
			return CreateDynamicMethod( "invoke", CallInfo.TargetType, Constants.ObjectType,
			                            CallInfo.IsStatic
			                            	? new[] { Constants.ObjectType.MakeArrayType() }
			                            	: new[] { Constants.ObjectType, Constants.ObjectType.MakeArrayType() } );
		}

		protected internal override Delegate CreateDelegate()
		{
			MethodInfo methodInfo = LookupUtils.GetMethod( CallInfo );
			byte paramArrayIndex = CallInfo.IsStatic ? (byte) 0 : (byte) 1;
			bool hasReturnType = methodInfo.ReturnType != Constants.VoidType;

			byte startUsableLocalIndex = 0;
			if( CallInfo.HasRefParam )
			{
				startUsableLocalIndex = CreateLocalsForByRefParams( paramArrayIndex, methodInfo );
					// create by_ref_locals from argument array
				Generator.DeclareLocal( hasReturnType
				                        	? methodInfo.ReturnType
				                        	: Constants.ObjectType ); // T result;
				GenerateInvocation( methodInfo, paramArrayIndex, (byte) (startUsableLocalIndex + 1) );
				if( hasReturnType )
				{
					Generator.stloc( startUsableLocalIndex ); // result = <stack>;
				}
				AssignByRefParamsToArray( paramArrayIndex ); // store by_ref_locals back to argument array
			}
			else
			{
				Generator.DeclareLocal( hasReturnType
				                        	? methodInfo.ReturnType
				                        	: Constants.ObjectType ); // T result;
				GenerateInvocation( methodInfo, paramArrayIndex, (byte) (startUsableLocalIndex + 1) );
				if( hasReturnType )
				{
					Generator.stloc( startUsableLocalIndex ); // result = <stack>;
				}
			}

			if( CallInfo.ShouldHandleInnerStruct )
			{
				StoreLocalToInnerStruct( (byte) (startUsableLocalIndex + 1) ); // ((ValueTypeHolder)this)).Value = tmpStr; 
			}
			if( hasReturnType )
			{
				Generator.ldloc( startUsableLocalIndex ) // push result;
					.boxIfValueType( methodInfo.ReturnType ); // box result;
			}
			else
			{
				Generator.ldnull.end(); // load null
			}
			Generator.ret();

			return Method.CreateDelegate( CallInfo.IsStatic
			                              	? typeof(StaticMethodInvoker)
			                              	: typeof(MethodInvoker) );
		}

		private void GenerateInvocation( MethodInfo methodInfo, byte paramArrayIndex, byte structLocalPosition )
		{
			if( !CallInfo.IsStatic )
			{
				Generator.ldarg_0.end(); // load arg-0 (this);
				if( CallInfo.ShouldHandleInnerStruct )
				{
					Generator.DeclareLocal( CallInfo.TargetType ); // TargetType tmpStr;
					LoadInnerStructToLocal( structLocalPosition ); // tmpStr = ((ValueTypeHolder)this)).Value;
				}
				else
				{
					Generator.castclass( CallInfo.TargetType ); // (TargetType)arg-0;
				}
			}
			PushParamsOrLocalsToStack( paramArrayIndex ); // push arguments and by_ref_locals
			Generator.call( CallInfo.IsStatic || CallInfo.IsTargetTypeStruct, methodInfo ); // call OR callvirt
		}
	}
}