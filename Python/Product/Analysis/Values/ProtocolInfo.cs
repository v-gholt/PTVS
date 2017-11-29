﻿// Python Tools for Visual Studio
// Copyright(c) Microsoft Corporation
// All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the License); you may not use
// this file except in compliance with the License. You may obtain a copy of the
// License at http://www.apache.org/licenses/LICENSE-2.0
//
// THIS CODE IS PROVIDED ON AN  *AS IS* BASIS, WITHOUT WARRANTIES OR CONDITIONS
// OF ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING WITHOUT LIMITATION ANY
// IMPLIED WARRANTIES OR CONDITIONS OF TITLE, FITNESS FOR A PARTICULAR PURPOSE,
// MERCHANTABLITY OR NON-INFRINGEMENT.
//
// See the Apache Version 2.0 License for specific language governing
// permissions and limitations under the License.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.PythonTools.Infrastructure;
using Microsoft.PythonTools.Interpreter;
using Microsoft.PythonTools.Parsing;
using Microsoft.PythonTools.Parsing.Ast;

namespace Microsoft.PythonTools.Analysis.Values {
    /// <summary>
    /// Represents a value that implements one or more protocols.
    /// </summary>
    class ProtocolInfo : AnalysisValue, IHasRichDescription {
        private readonly List<Protocol> _protocols;
        private readonly ReferenceDict _references;

        private IAnalysisSet _instance;
        private Dictionary<string, IAnalysisSet> _members;
        private BuiltinTypeId? _typeId;

        public ProtocolInfo(ProjectEntry declaringModule) {
            _protocols = new List<Protocol>();
            DeclaringModule = declaringModule;
            DeclaringVersion = declaringModule?.AnalysisVersion ?? -1;
            _references = new ReferenceDict();
        }

        public void AddProtocol(Protocol p) {
            _protocols.Add(p);
            _instance = null;
            _members = null;
            _typeId = null;
        }

        public override string Name => string.Join(", ", _protocols.Select(p => p.Name));
        public override IEnumerable<OverloadResult> Overloads => _protocols.SelectMany(p => p.Overloads);
        public override PythonMemberType MemberType => PythonMemberType.Instance;
        public override IPythonProjectEntry DeclaringModule { get; }
        public override int DeclaringVersion { get; }

        public override IEnumerable<IAnalysisSet> Mro => base.Mro;

        public override AnalysisUnit AnalysisUnit => base.AnalysisUnit;

        internal override BuiltinTypeId TypeId {
            get {
                if (_typeId == null) {
                    foreach (var p in _protocols) {
                        if (p.TypeId == BuiltinTypeId.Unknown) {
                            continue;
                        }

                        if (_typeId == null) {
                            _typeId = p.TypeId;
                        } else if (_typeId != p.TypeId) {
                            _typeId = BuiltinTypeId.Unknown;
                            break;
                        }
                    }
                    if (_typeId == null) {
                        _typeId = BuiltinTypeId.Unknown;
                    }
                }
                return _typeId.GetValueOrDefault();
            }
        }

        internal override void AddReference(Node node, AnalysisUnit analysisUnit) {
            _references.GetReferences(analysisUnit.ProjectEntry).AddReference(new EncodedLocation(analysisUnit, node));
        }

        public override IEnumerable<LocationInfo> Locations {
            get {
                ReferenceList defns;
                if (!_references.TryGetValue(DeclaringModule, out defns)) {
                    return Enumerable.Empty<LocationInfo>();
                }
                return defns.Definitions.Select(l => l.GetLocationInfo()).Where(l => l != null);
            }
        }

        internal override IEnumerable<LocationInfo> References => _references.AllReferences;

        public override void AugmentAssign(AugmentedAssignStatement node, AnalysisUnit unit, IAnalysisSet value) {
            foreach (var p in _protocols) {
                p.AugmentAssign(node, unit, value);
            }
        }

        public override IAnalysisSet Await(Node node, AnalysisUnit unit) {
            return AnalysisSet.UnionAll(_protocols.Select(p => p.Await(node, unit)));
        }

        public override IAnalysisSet BinaryOperation(Node node, AnalysisUnit unit, PythonOperator operation, IAnalysisSet rhs) {
            return AnalysisSet.UnionAll(_protocols.Select(p => p.BinaryOperation(node, unit, operation, rhs)));
        }

        public override IAnalysisSet Call(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            return AnalysisSet.UnionAll(_protocols.Select(p => p.Call(node, unit, args, keywordArgNames)));
        }

        public override void DeleteMember(Node node, AnalysisUnit unit, string name) {
            foreach (var p in _protocols) {
                p.DeleteMember(node, unit, name);
            }
        }

        public override IDictionary<string, IAnalysisSet> GetAllMembers(IModuleContext moduleContext, GetMemberOptions options = GetMemberOptions.None) {
            if (_members != null) {
                return _members;
            }

            var members = new Dictionary<string, IAnalysisSet>();
            foreach (var p in _protocols) {
                foreach (var kv in p.GetAllMembers(moduleContext, options).MaybeEnumerate()) {
                    if (members.TryGetValue(kv.Key, out var existing)) {
                        members[kv.Key] = existing.Union(kv.Value);
                    } else {
                        members[kv.Key] = kv.Value;
                    }
                }
            }
            _members = _members ?? members;
            return _members;
        }

        public override IAnalysisSet GetAsyncEnumeratorTypes(Node node, AnalysisUnit unit) {
            return AnalysisSet.UnionAll(_protocols.Select(p => p.GetAsyncEnumeratorTypes(node, unit)));
        }

        public override IAnalysisSet GetAsyncIterator(Node node, AnalysisUnit unit) {
            return AnalysisSet.UnionAll(_protocols.Select(p => p.GetAsyncIterator(node, unit)));
        }

        public override object GetConstantValue() {
            return _protocols.Select(p => p.GetConstantValue()).FirstOrDefault(p => p != null) ?? base.GetConstantValue();
        }

        public override IAnalysisSet GetDescriptor(Node node, AnalysisValue instance, AnalysisValue context, AnalysisUnit unit) {
            return AnalysisSet.UnionAll(_protocols.Select(p => p.GetDescriptor(node, instance, context, unit)));
        }

        public override IAnalysisSet GetDescriptor(PythonAnalyzer projectState, AnalysisValue instance, AnalysisValue context) {
            return AnalysisSet.UnionAll(_protocols.Select(p => p.GetDescriptor(projectState, instance, context)));
        }

        public override IAnalysisSet GetEnumeratorTypes(Node node, AnalysisUnit unit) {
            return AnalysisSet.UnionAll(_protocols.Select(p => p.GetEnumeratorTypes(node, unit)));
        }

        public override IAnalysisSet GetIndex(Node node, AnalysisUnit unit, IAnalysisSet index) {
            return AnalysisSet.UnionAll(_protocols.Select(p => p.GetIndex(node, unit, index)));
        }

        public override IAnalysisSet GetInstanceType() {
            _instance = _instance ?? AnalysisSet.UnionAll(_protocols.Select(p => p.GetInstanceType()));
            return _instance;
        }

        public override IAnalysisSet GetIterator(Node node, AnalysisUnit unit) {
            return AnalysisSet.UnionAll(_protocols.Select(p => p.GetIterator(node, unit)));
        }

        public override int? GetLength() {
            return _protocols.Select(p => p.GetLength()).FirstOrDefault(v => v.HasValue) ?? base.GetLength();
        }

        public override IAnalysisSet GetMember(Node node, AnalysisUnit unit, string name) {
            return AnalysisSet.UnionAll(_protocols.Select(p => p.GetMember(node, unit, name)));
        }

        public override IAnalysisSet GetTypeMember(Node node, AnalysisUnit unit, string name) {
            return AnalysisSet.UnionAll(_protocols.Select(p => p.GetTypeMember(node, unit, name)));
        }

        public override IAnalysisSet ReverseBinaryOperation(Node node, AnalysisUnit unit, PythonOperator operation, IAnalysisSet rhs) {
            return AnalysisSet.UnionAll(_protocols.Select(p => p.ReverseBinaryOperation(node, unit, operation, rhs)));
        }

        public override void SetIndex(Node node, AnalysisUnit unit, IAnalysisSet index, IAnalysisSet value) {
            foreach (var p in _protocols) {
                p.SetIndex(node, unit, index, value);
            }
        }

        public override void SetMember(Node node, AnalysisUnit unit, string name, IAnalysisSet value) {
            foreach (var p in _protocols) {
                p.SetMember(node, unit, name, value);
            }
        }

        public override IAnalysisSet UnaryOperation(Node node, AnalysisUnit unit, PythonOperator operation) {
            return AnalysisSet.UnionAll(_protocols.Select(p => p.UnaryOperation(node, unit, operation)));
        }

        internal override bool UnionEquals(AnalysisValue av, int strength) {
            return av.GetType() == GetType();
        }

        internal override int UnionHashCode(int strength) {
            return GetType().GetHashCode();
        }

        internal override AnalysisValue UnionMergeTypes(AnalysisValue av, int strength) {
            // TODO: Merge properly
            return this;
        }

        public virtual IEnumerable<KeyValuePair<string, string>> GetRichDescription() {
            if (_protocols.Any()) {
                bool skipComma = true;
                foreach (var p in _protocols) {
                    if (!skipComma) {
                        yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Comma, ", ");
                    }
                    skipComma = false;
                    foreach (var d in p.GetRichDescription()) {
                        yield return d;
                    }
                }
            } else {
                yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Name, "<unknown>");
            }
            yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.EndOfDeclaration, "");
        }
    }

    abstract class Protocol : AnalysisValue, IHasRichDescription {
        public readonly ProtocolInfo Self;
        private Dictionary<string, IAnalysisSet> _members;

        public Protocol(ProtocolInfo self) {
            Self = self;
        }

        protected void EnsureMembers() {
            if (_members == null) {
                var m = new Dictionary<string, IAnalysisSet>();
                EnsureMembers(m);
                _members = m;
            }
        }

        protected virtual void EnsureMembers(IDictionary<string, IAnalysisSet> members) {
        }

        protected IAnalysisSet MakeMethod(string qualname, IAnalysisSet returnValue) {
            return MakeMethod(qualname, Array.Empty<IAnalysisSet>(), returnValue);
        }

        protected IAnalysisSet MakeMethod(string qualname, IReadOnlyList<IAnalysisSet> arguments, IAnalysisSet returnValue) {
            var v = new ProtocolInfo(Self.DeclaringModule as ProjectEntry);
            v.AddProtocol(new CallableProtocol(v, qualname, arguments, returnValue));
            return v;
        }

        public override PythonMemberType MemberType => PythonMemberType.Unknown;

        public override IDictionary<string, IAnalysisSet> GetAllMembers(IModuleContext moduleContext, GetMemberOptions options = GetMemberOptions.None) {
            EnsureMembers();
            return _members;
        }

        public override IAnalysisSet GetMember(Node node, AnalysisUnit unit, string name) {
            var res = base.GetMember(node, unit, name);
            EnsureMembers();
            if (_members.TryGetValue(name, out var m)) {
                return m;
            }
            return res;
        }

        public virtual IEnumerable<KeyValuePair<string, string>> GetRichDescription() {
            yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Name, Name);
        }
    }

    class CallableProtocol : Protocol {
        private readonly Lazy<OverloadResult[]> _overloads;

        public CallableProtocol(ProtocolInfo self, string qualname, IReadOnlyList<IAnalysisSet> arguments, IAnalysisSet returnType)
            : base(self) {
            Name = qualname ?? "callable";
            Arguments = arguments;
            ReturnType = returnType;
            _overloads = new Lazy<OverloadResult[]>(GenerateOverloads);
        }

        public override string Name { get; }

        internal override BuiltinTypeId TypeId => BuiltinTypeId.Function;

        protected override void EnsureMembers(IDictionary<string, IAnalysisSet> members) {
            members["__call__"] = Self;
        }

        private OverloadResult[] GenerateOverloads() {
            return new[] {
                new OverloadResult(Arguments.Select(ToParameterResult).ToArray(), Name)
            };
        }

        private ParameterResult ToParameterResult(IAnalysisSet set, int i) {
            return new ParameterResult($"${i}", $"Parameter {i}", set.ToString());
        }

        public IReadOnlyList<IAnalysisSet> Arguments { get; }
        public IAnalysisSet ReturnType { get; }

        public override IAnalysisSet Call(Node node, AnalysisUnit unit, IAnalysisSet[] args, NameExpression[] keywordArgNames) {
            var def = base.Call(node, unit, args, keywordArgNames);
            return ReturnType ?? def;
        }

        public override IEnumerable<OverloadResult> Overloads => _overloads.Value;

        public override IEnumerable<KeyValuePair<string, string>> GetRichDescription() {
            yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Name, Name);
            // TODO: Generate correct signature using code from FunctionInfo
            yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, "()");
        }
    }

    class IterableProtocol : Protocol {
        private readonly IAnalysisSet _iterator;
        private readonly IAnalysisSet _yielded;

        public IterableProtocol(ProtocolInfo self, IAnalysisSet yielded) : base(self) {
            _yielded = yielded;

            var iterator = new ProtocolInfo(Self.DeclaringModule as ProjectEntry);
            iterator.AddProtocol(new IteratorProtocol(iterator, _yielded));
            _iterator = iterator;
        }

        private void EnsureMembers(Dictionary<string, IAnalysisSet> members) {
            members["__iter__"] = MakeMethod("__iter__", _iterator);
        }

        public override IAnalysisSet GetIterator(Node node, AnalysisUnit unit) {
            return _iterator;
        }

        public override string Name => "iterable";

        public override IEnumerable<KeyValuePair<string, string>> GetRichDescription() {
            yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Name, Name);
            if (_yielded.Any()) {
                yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, "[");
                bool skipComma = true;
                foreach (var d in _yielded.Select(v => v.ShortDescription).OrderBy(k => k)) {
                    if (!skipComma) {
                        yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Comma, ", ");
                    }
                    skipComma = false;
                    yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, d);
                }
                yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, "]");
            }
        }
    }

    class IteratorProtocol : Protocol {
        private readonly IAnalysisSet _yielded;

        public IteratorProtocol(ProtocolInfo self, IAnalysisSet yielded) : base(self) {
            _yielded = yielded;
        }

        private void EnsureMembers(Dictionary<string, IAnalysisSet> members) {
            if (Self.DeclaringModule?.Tree?.LanguageVersion.Is3x() ?? true) {
                members["__next__"] = MakeMethod("__next__", _yielded);
            } else {
                members["next"] = MakeMethod("next", _yielded);
            }
            members["__iter__"] = MakeMethod("__iter__", Self);
        }

        public override IAnalysisSet GetEnumeratorTypes(Node node, AnalysisUnit unit) {
            return _yielded;
        }

        public override string Name => "iterator";

        public override IEnumerable<KeyValuePair<string, string>> GetRichDescription() {
            yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Name, Name);
            if (_yielded.Any()) {
                yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, "[");
                bool skipComma = true;
                foreach (var d in _yielded.Select(v => v.ShortDescription).OrderBy(k => k)) {
                    if (!skipComma) {
                        yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Comma, ", ");
                    }
                    skipComma = false;
                    yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, d);
                }
                yield return new KeyValuePair<string, string>(WellKnownRichDescriptionKinds.Misc, "]");
            }
        }
    }
}
