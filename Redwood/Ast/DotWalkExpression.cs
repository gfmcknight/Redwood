﻿using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Redwood.Instructions;
using Redwood.Runtime;

namespace Redwood.Ast
{
    public class DotWalkExpression : Expression
    {
        public Expression Chain { get; set; }
        public NameExpression Element { get; set; }

        internal RedwoodType KnownType { get; set; }

        internal override void Bind(Binder binder)
        {
            Chain.Bind(binder);
            RedwoodType chainType = Chain.GetKnownType();
            if (chainType == null)
            {
                return;
            }

            if (chainType.CSharpType == null)
            {
                KnownType = chainType.slotTypes?[chainType.slotMap[Element.Name]];
            }
            else
            {
                PropertyInfo property;
                FieldInfo field;
                MemberResolver.TryResolveMember(chainType, Element.Name, false, out property, out field);
                if (property != null)
                {
                    KnownType = RedwoodType.GetForCSharpType(property.PropertyType);
                }
                if (field != null)
                {
                    KnownType = RedwoodType.GetForCSharpType(field.FieldType);
                }
            }
        }

        internal override IEnumerable<Instruction> Compile()
        {
            List<Instruction> instructions = new List<Instruction>();
            instructions.AddRange(Chain.Compile());
            RedwoodType chainType = Chain.GetKnownType();
            if (chainType == null)
            {
                instructions.Add(new LookupExternalMemberInstruction(Element.Name, null));
            }
            else if (chainType.CSharpType == null)
            {
                if (!chainType.slotMap.ContainsKey(Element.Name))
                {
                    throw new NotImplementedException();
                }

                instructions.Add(new LookupDirectMemberInstruction(chainType.slotMap[Element.Name]));
            }
            else
            {
                // TODO: Should this be a special reflected instruction?
                instructions.Add(new LookupExternalMemberInstruction(Element.Name, chainType));
            }
            return instructions;
        }

        internal override IEnumerable<NameExpression> Walk()
        {
            return Chain.Walk();
        }

        public override RedwoodType GetKnownType()
        {
            return KnownType;
        }

        internal override IEnumerable<Instruction> CompileAssignmentTarget(
            List<Variable> temporaryVariables)
        {
            List<Instruction> instructions = new List<Instruction>();
            instructions.Add(Compiler.CompileVariableAssign(temporaryVariables[0]));
            instructions.AddRange(Chain.Compile());

            RedwoodType chainType = Chain.GetKnownType();
            if (chainType == null)
            {
                throw new NotImplementedException();
            }

            if (chainType.CSharpType == null)
            {
                int slot = chainType.slotMap[Element.Name];
                instructions.Add(
                    new AssignDirectMemberInstruction(
                        slot,
                        temporaryVariables[0].Location
                    )
                );
            }
            else
            {
                PropertyInfo property;
                FieldInfo field;
                MemberResolver.TryResolveMember(
                    chainType,
                    Element.Name,
                    false,
                    out property,
                    out field);

                if (property != null)
                {
                    instructions.Add(
                        new AssignExternalPropertyInstruction(
                            property,
                            temporaryVariables[0].Location
                        )
                    );
                }

                if (field != null)
                {
                    instructions.Add(
                        new AssignExternalFieldInstruction(
                            field,
                            temporaryVariables[0].Location
                        )
                    );
                }
            }

            // We have to satisfy that (x.y = 3) == 3
            instructions.Add(Compiler.CompileVariableLookup(temporaryVariables[0]));
            return instructions;
        }
    }
}
