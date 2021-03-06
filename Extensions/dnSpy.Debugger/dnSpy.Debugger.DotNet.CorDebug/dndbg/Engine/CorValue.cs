﻿/*
    Copyright (C) 2014-2017 de4dot@gmail.com

    This file is part of dnSpy

    dnSpy is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    dnSpy is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with dnSpy.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using dndbg.COM.CorDebug;
using dnlib.DotNet;

namespace dndbg.Engine {
	sealed class CorValue : COMObject<ICorDebugValue>, IEquatable<CorValue> {
		/// <summary>
		/// true if it's a <see cref="ICorDebugGenericValue"/>
		/// </summary>
		public bool IsGeneric => obj is ICorDebugGenericValue;

		/// <summary>
		/// true if it's a <see cref="ICorDebugReferenceValue"/>
		/// </summary>
		public bool IsReference => obj is ICorDebugReferenceValue;

		/// <summary>
		/// true if it's a <see cref="ICorDebugHandleValue"/>
		/// </summary>
		public bool IsHandle => obj is ICorDebugHandleValue;

		/// <summary>
		/// true if it's a <see cref="ICorDebugHeapValue"/>, <see cref="ICorDebugArrayValue"/>,
		/// <see cref="ICorDebugBoxValue"/> or <see cref="ICorDebugStringValue"/>
		/// </summary>
		public bool IsHeap => obj is ICorDebugHeapValue;

		public bool IsHeap2 => obj is ICorDebugHeapValue2;

		/// <summary>
		/// true if it's a <see cref="ICorDebugArrayValue"/>
		/// </summary>
		public bool IsArray => obj is ICorDebugArrayValue;

		/// <summary>
		/// true if it's a <see cref="ICorDebugBoxValue"/>
		/// </summary>
		public bool IsBox => obj is ICorDebugBoxValue;

		/// <summary>
		/// true if it's a <see cref="ICorDebugStringValue"/>
		/// </summary>
		public bool IsString => obj is ICorDebugStringValue;

		/// <summary>
		/// true if it's a <see cref="ICorDebugObjectValue"/>
		/// </summary>
		public bool IsObject => obj is ICorDebugObjectValue;

		/// <summary>
		/// true if it's a <see cref="ICorDebugContext"/>
		/// </summary>
		public bool IsContext => obj is ICorDebugContext;

		/// <summary>
		/// true if it's a <see cref="ICorDebugComObjectValue"/>, ie., an RCW (Runtime Callable Wrapper)
		/// </summary>
		public bool IsComObject => obj is ICorDebugComObjectValue;

		/// <summary>
		/// true if it's a <see cref="ICorDebugExceptionObjectValue"/>
		/// </summary>
		public bool IsExceptionObject => obj is ICorDebugExceptionObjectValue;

		/// <summary>
		/// Gets the element type of this value
		/// </summary>
		public CorElementType ElementType => elemType;
		readonly CorElementType elemType;

		/// <summary>
		/// Returns the enum underlying type if it's an enum, else <see cref="ElementType"/> is returned
		/// </summary>
		public CorElementType TypeOrEnumUnderlyingType => ExactType?.TypeOrEnumUnderlyingType ?? ElementType;

		/// <summary>
		/// Gets the size of the value
		/// </summary>
		public ulong Size => size;
		readonly ulong size;

		/// <summary>
		/// Gets the address of the value or 0 if it's not available, eg. it could be in a register
		/// </summary>
		public ulong Address => address;
		readonly ulong address;

		/// <summary>
		/// Gets the class or null if it's not a <see cref="ICorDebugObjectValue"/> or
		/// <see cref="ICorDebugContext"/>
		/// </summary>
		public CorClass Class {
			get {
				var o = obj as ICorDebugObjectValue;
				if (o == null)
					return null;
				int hr = o.GetClass(out var cls);
				return hr < 0 || cls == null ? null : new CorClass(cls);
			}
		}

		/// <summary>
		/// Gets the type or null
		/// </summary>
		public CorType ExactType {
			get {
				var v2 = obj as ICorDebugValue2;
				if (v2 == null)
					return null;
				int hr = v2.GetExactType(out var type);
				return hr < 0 || type == null ? null : new CorType(type);
			}
		}

		/// <summary>
		/// true if this is a <see cref="ICorDebugReferenceValue"/> and the reference is null
		/// </summary>
		public bool IsNull {
			get {
				var r = obj as ICorDebugReferenceValue;
				if (r == null)
					return false;
				int hr = r.IsNull(out int isn);
				return hr >= 0 && isn != 0;
			}
		}

		/// <summary>
		/// Gets/sets the address to which <see cref="ICorDebugReferenceValue"/> points
		/// </summary>
		public ulong ReferenceAddress {
			get {
				var r = obj as ICorDebugReferenceValue;
				if (r == null)
					return 0;
				int hr = r.GetValue(out ulong addr);
				return hr < 0 ? 0 : addr;
			}
			set {
				var r = obj as ICorDebugReferenceValue;
				if (r == null)
					return;
				int hr = r.SetValue(value);
			}
		}

		/// <summary>
		/// Gets the handle type if it's a <see cref="ICorDebugHandleValue"/>
		/// </summary>
		public CorDebugHandleType HandleType {
			get {
				var h = obj as ICorDebugHandleValue;
				if (h == null)
					return 0;
				int hr = h.GetHandleType(out var type);
				return hr < 0 ? 0 : type;
			}
		}

		/// <summary>
		/// Gets the dereferenced value to which <see cref="ICorDebugReferenceValue"/> points or null
		/// </summary>
		public CorValue DereferencedValue {
			get {
				var r = obj as ICorDebugReferenceValue;
				if (r == null)
					return null;
				int hr = r.Dereference(out var value);
				return hr < 0 || value == null ? null : new CorValue(value);
			}
		}

		/// <summary>
		/// Gets the type of the array's elements or <see cref="CorElementType.End"/> if it's not an array
		/// </summary>
		public CorElementType ArrayElementType {
			get {
				var a = obj as ICorDebugArrayValue;
				if (a == null)
					return CorElementType.End;
				int hr = a.GetElementType(out var etype);
				return hr < 0 ? 0 : etype;
			}
		}

		/// <summary>
		/// Gets the rank of the array or 0 if it's not an array
		/// </summary>
		public uint Rank {
			get {
				var a = obj as ICorDebugArrayValue;
				if (a == null)
					return 0;
				int hr = a.GetRank(out uint rank);
				return hr < 0 ? 0 : rank;
			}
		}

		/// <summary>
		/// Gets the number of elements in the array or 0 if it's not an array
		/// </summary>
		public uint ArrayCount {
			get {
				var a = obj as ICorDebugArrayValue;
				if (a == null)
					return 0;
				int hr = a.GetCount(out uint count);
				return hr < 0 ? 0 : count;
			}
		}

		/// <summary>
		/// Gets the dimensions or null if it's not an array
		/// </summary>
		public unsafe uint[] Dimensions {
			get {
				var a = obj as ICorDebugArrayValue;
				if (a == null)
					return null;
				uint[] dims = new uint[Rank];
				fixed (uint* p = &dims[0]) {
					int hr = a.GetDimensions((uint)dims.Length, new IntPtr(p));
				}
				return dims;
			}
		}

		/// <summary>
		/// true if the array has base indices
		/// </summary>
		public bool HasBaseIndicies {
			get {
				var a = obj as ICorDebugArrayValue;
				if (a == null)
					return false;
				int hr = a.HasBaseIndicies(out int has);
				return hr >= 0 && has != 0;
			}
		}

		/// <summary>
		/// Gets all base indicies or null if it's not an array
		/// </summary>
		public unsafe uint[] BaseIndicies {
			get {
				var a = obj as ICorDebugArrayValue;
				if (a == null)
					return null;
				uint[] indicies = new uint[Rank];
				fixed (uint* p = &indicies[0]) {
					int hr = a.GetBaseIndicies((uint)indicies.Length, new IntPtr(p));
				}
				return indicies;
			}
		}

		/// <summary>
		/// Gets the boxed object value or null if none. It's a <see cref="ICorDebugObjectValue"/>
		/// </summary>
		public CorValue BoxedValue {
			get {
				var b = obj as ICorDebugBoxValue;
				if (b == null)
					return null;
				int hr = b.GetObject(out var value);
				return hr < 0 || value == null ? null : new CorValue(value);
			}
		}

		/// <summary>
		/// Gets the length of the string in characters or 0 if it's not a <see cref="ICorDebugStringValue"/>
		/// </summary>
		public uint StringLength {
			get {
				var s = obj as ICorDebugStringValue;
				if (s == null)
					return 0;
				int hr = s.GetLength(out uint len);
				return hr < 0 ? 0 : len;
			}
		}

		/// <summary>
		/// Gets the string or null if it's not a <see cref="ICorDebugStringValue"/>
		/// </summary>
		public unsafe string String {
			get {
				var s = obj as ICorDebugStringValue;
				if (s == null)
					return null;
				uint len = StringLength;
				if (len == 0)
					return string.Empty;
				var chars = new char[len];
				int hr;
				fixed (char* p = &chars[0]) {
					hr = s.GetString((uint)chars.Length, out len, new IntPtr(p));
				}
				if (hr < 0)
					return null;
				return new string(chars);
			}
		}

		/// <summary>
		/// true if this is a <see cref="ICorDebugObjectValue"/> and it's a value type
		/// </summary>
		public bool IsValueClass {
			get {
				var o = obj as ICorDebugObjectValue;
				if (o == null)
					return false;
				int hr = o.IsValueClass(out int i);
				return hr >= 0 && i != 0;
			}
		}

		/// <summary>
		/// Gets the value. Only values of simple types are currently returned: boolean, integers,
		/// floating points, decimal, string and null.
		/// </summary>
		public CorValueResult Value => CorValueReader.ReadSimpleTypeValue(this);

		/// <summary>
		/// true if the value has been neutered, eg. because Continue() was called
		/// </summary>
		public bool IsNeutered {
			get {
				// If it's neutered, at least one of these (most likely GetType()) should fail.
				int hr = obj.GetType(out var type);
				if (hr == CordbgErrors.CORDBG_E_OBJECT_NEUTERED)
					return true;
				Debug.Assert(hr == 0);
				hr = obj.GetAddress(out ulong addr);
				if (hr == CordbgErrors.CORDBG_E_OBJECT_NEUTERED)
					return true;
				Debug.Assert(hr == 0);
				hr = obj.GetSize(out uint size);
				if (hr == CordbgErrors.CORDBG_E_OBJECT_NEUTERED)
					return true;
				Debug.Assert(hr == 0);

				return false;
			}
		}

		public CorValue(ICorDebugValue value)
			: base(value) {
			int hr = value.GetType(out elemType);
			if (hr < 0)
				elemType = CorElementType.End;

			bool initdSize = false;
			if (value is ICorDebugValue3 v3)
				initdSize = v3.GetSize64(out size) == 0;
			if (!initdSize) {
				hr = value.GetSize(out uint size32);
				if (hr < 0)
					size32 = 0;
				size = size32;
			}

			hr = value.GetAddress(out address);
			if (hr < 0)
				address = 0;
		}

		/// <summary>
		/// Disposes the handle if it's a <see cref="ICorDebugHandleValue"/>
		/// </summary>
		/// <returns></returns>
		public bool DisposeHandle() {
			var h = obj as ICorDebugHandleValue;
			if (h == null)
				return false;
			int hr = h.Dispose();
			bool success = hr == 0 || hr == CordbgErrors.CORDBG_E_OBJECT_NEUTERED;
			Debug.Assert(success);
			return success;
		}

		/// <summary>
		/// Gets the value at a specified index in the array or null. The array is treated as a
		/// zero-based, single-dimensional array
		/// </summary>
		/// <param name="index">Index of element</param>
		/// <param name="hr">Updated with HRESULT</param>
		/// <returns></returns>
		public CorValue GetElementAtPosition(uint index, out int hr) {
			var a = obj as ICorDebugArrayValue;
			if (a == null) {
				hr = -1;
				return null;
			}
			hr = a.GetElementAtPosition(index, out var value);
			return hr < 0 || value == null ? null : new CorValue(value);
		}

		/// <summary>
		/// Gets the value at a specified index in the array or null. The array is treated as a
		/// zero-based, single-dimensional array
		/// </summary>
		/// <param name="index">Index of element</param>
		/// <param name="hr">Updated with HRESULT</param>
		/// <returns></returns>
		public CorValue GetElementAtPosition(int index, out int hr) => GetElementAtPosition((uint)index, out hr);

		/// <summary>
		/// Gets the value of a field or null if it's not a <see cref="ICorDebugObjectValue"/>
		/// </summary>
		/// <param name="cls">Class</param>
		/// <param name="token">Token of field in <paramref name="cls"/></param>
		/// <returns></returns>
		public CorValue GetFieldValue(CorClass cls, uint token) => GetFieldValue(cls, token, out int hr);

		/// <summary>
		/// Gets the value of a field or null if it's not a <see cref="ICorDebugObjectValue"/>
		/// </summary>
		/// <param name="cls">Class</param>
		/// <param name="token">Token of field in <paramref name="cls"/></param>
		/// <param name="hr">Updated with HRESULT</param>
		/// <returns></returns>
		public CorValue GetFieldValue(CorClass cls, uint token, out int hr) {
			var o = obj as ICorDebugObjectValue;
			if (o == null || cls == null) {
				hr = -1;
				return null;
			}
			hr = o.GetFieldValue(cls.RawObject, token, out var value);
			return hr < 0 || value == null ? null : new CorValue(value);
		}

		/// <summary>
		/// Gets the value of a field. Returns null if field wasn't found or there was another error
		/// </summary>
		/// <param name="name">Name of field</param>
		/// <param name="checkBaseClasses">true to check base classes</param>
		/// <returns></returns>
		public CorValue GetFieldValue(string name, bool checkBaseClasses = true) {
			var self = this;
			if (self.IsReference) {
				self = self.DereferencedValue;
				if (self == null)
					return null;
			}

			var type = self.ExactType;
			if (type == null)
				return null;
			var info = type.GetFields(checkBaseClasses).FirstOrDefault(a => a.Name == name);
			if (info == null)
				return null;
			return self.GetFieldValue(info.OwnerType?.Class, info.Token);
		}

		/// <summary>
		/// Creates a handle to this <see cref="ICorDebugHeapValue"/>. The returned value is a
		/// <see cref="ICorDebugHandleValue"/>.
		/// </summary>
		/// <param name="type">Type</param>
		/// <returns></returns>
		public CorValue CreateHandle(CorDebugHandleType type) {
			var h2 = obj as ICorDebugHeapValue2;
			if (h2 == null)
				return null;
			int hr = h2.CreateHandle(type, out var value);
			return hr < 0 || value == null ? null : new CorValue(value);
		}

		/// <summary>
		/// Writes a new value. Can be called if <see cref="IsGeneric"/> is true
		/// </summary>
		/// <param name="data">Data</param>
		/// <param name="process">Process to use if first method call fails</param>
		/// <returns></returns>
		public unsafe int WriteGenericValue(byte[] data, CorProcess process = null) {
			if (data == null || (uint)data.Length != Size)
				return -1;
			var g = obj as ICorDebugGenericValue;
			if (g == null)
				return -1;
			int hr;
			fixed (byte* p = &data[0]) {
				// This sometimes fails with CORDBG_E_CLASS_NOT_LOADED (ImmutableArray<T>, debugging VS2017).
				// If it fails, use process.WriteMemory().
				hr = g.SetValue(new IntPtr(p));
				if (hr < 0 && process != null) {
					hr = process.WriteMemory(address, data, 0, data.Length, out var sizeWritten);
					if (sizeWritten != data.Length && hr >= 0)
						hr = -1;
				}
			}
			return hr;
		}

		/// <summary>
		/// Reads the data. Can be called if <see cref="IsGeneric"/> is true. Returns null if there
		/// was an error.
		/// </summary>
		/// <returns></returns>
		public unsafe byte[] ReadGenericValue() {
			var g = obj as ICorDebugGenericValue;
			if (g == null)
				return null;
			var data = new byte[Size];
			int hr;
			fixed (byte* p = &data[0]) {
				hr = g.GetValue(new IntPtr(p));
			}
			return hr < 0 ? null : data;
		}

		/// <summary>
		/// Gets the nullable value's value field. Returns true if it's a nullable type, false if
		/// it's not a nullable type.
		/// </summary>
		/// <param name="value">Updated with the value of the nullable field or null if the nullable
		/// is null or if it's not a nullable value</param>
		/// <returns></returns>
		public bool GetNullableValue(out CorValue value) {
			value = null;
			if (!Utils.GetSystemNullableFields(ExactType, out var hasValueInfo, out var valueInfo))
				return false;
			var type = ExactType;
			if (type == null)
				return false;
			var cls = type.Class;
			if (cls == null)
				return false;

			var hasValueValue = GetFieldValue(cls, hasValueInfo.Token);
			if (hasValueValue == null)
				return false;
			var hasValueRes = hasValueValue.Value;
			if (!hasValueRes.IsValid || !(hasValueRes.Value is bool))
				return false;
			if (!(bool)hasValueRes.Value)
				return true;

			var valueValue = GetFieldValue(cls, valueInfo.Token);
			if (valueValue == null)
				return false;
			value = valueValue;
			return true;
		}

		public static bool operator ==(CorValue a, CorValue b) {
			if (ReferenceEquals(a, b))
				return true;
			if (ReferenceEquals(a, null) || ReferenceEquals(b, null))
				return false;
			return a.Equals(b);
		}

		public static bool operator !=(CorValue a, CorValue b) => !(a == b);
		public bool Equals(CorValue other) => !ReferenceEquals(other, null) && RawObject == other.RawObject;
		public override bool Equals(object obj) => Equals(obj as CorValue);
		public override int GetHashCode() => RawObject.GetHashCode();

		public T Write<T>(T output, TypeFormatterFlags flags, Func<DnEval> getEval = null) where T : ITypeOutput {
			new TypeFormatter(output, flags, getEval).Write(this);
			return output;
		}

		public T WriteType<T>(T output, TypeSig ts, IList<CorType> typeArgs, IList<CorType> methodArgs, TypeFormatterFlags flags, Func<DnEval> getEval = null) where T : ITypeOutput {
			new TypeFormatter(output, flags, getEval).Write(ts, typeArgs, methodArgs);
			return output;
		}

		public T WriteType<T>(T output, CorType type, TypeFormatterFlags flags, Func<DnEval> getEval = null) where T : ITypeOutput {
			new TypeFormatter(output, flags, getEval).Write(type);
			return output;
		}

		public T WriteType<T>(T output, CorClass cls, TypeFormatterFlags flags, Func<DnEval> getEval = null) where T : ITypeOutput {
			new TypeFormatter(output, flags, getEval).Write(cls);
			return output;
		}

		public string ToString(TypeFormatterFlags flags, Func<DnEval> getEval = null) => Write(new StringBuilderTypeOutput(), flags, getEval).ToString();
		public string ToString(Func<DnEval> getEval) => ToString(TypeFormatterFlags.Default, getEval);
		public override string ToString() => ToString(TypeFormatterFlags.Default);
	}
}
