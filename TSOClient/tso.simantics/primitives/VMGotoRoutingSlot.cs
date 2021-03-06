﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TSO.Files.utils;
using TSO.Simantics.engine.utils;
using TSO.Simantics.engine.scopes;
using Microsoft.Xna.Framework;
using tso.world.model;

namespace TSO.Simantics.engine.primitives
{
    public class VMGotoRoutingSlot : VMPrimitiveHandler {
        public override VMPrimitiveExitCode Execute(VMStackFrame context)
        {
            var operand = context.GetCurrentOperand<VMGotoRoutingSlotOperand>();
            
            var slot = VMMemory.GetSlot(context, operand.Type, operand.Data);
            var obj = context.StackObject;
            var avatar = context.Caller;


            /**
             * How we should be going about this:
             * 
             * Step 1: Evaluate possible positons for sim to route to
             * Step 2: Eliminate all positions intersected by an object that does not allow person intersection
             * Step 3: Evaluate routes to all positions, choose shortest route and eliminate all positions that we cannot route to (ignoring people).
             * Step 4: Route to position. Stop when the next tile has a person in it and ask them to move if possible.
             *
             **/

            //Routing slots must be type 3.
            if (slot.Type == 3){
                var possibleTargets = VMSlotParser.FindAvaliableLocations(obj, slot, context.VM.Context);
                if (possibleTargets.Count == 0){
                    return VMPrimitiveExitCode.GOTO_FALSE;
                }

                //TODO: Route finding and pick best route
                var target = possibleTargets[0];

                var pathFinder = context.Thread.PushNewPathFinder(context, possibleTargets);
                if (pathFinder != null) return VMPrimitiveExitCode.CONTINUE;
                else return VMPrimitiveExitCode.GOTO_FALSE;

                //var test = new VMPathFinder();
                //test.Caller = context.Caller;
                //test.Routine = context.Routine;
                //test.InitRoutes(possibleTargets);

                //avatar.SetPersonData(TSO.Simantics.model.VMPersonDataVariable.RouteEntryFlags, (short)target.Flags);
                //avatar.Direction = (Direction)target.Flags;
                //avatar.Position = new Vector3(target.Position.X + 0.5f, target.Position.Y + 0.5f, 0.0f);
            }

            return VMPrimitiveExitCode.GOTO_TRUE_NEXT_TICK;
        }
    }

    public class VMGotoRoutingSlotOperand : VMPrimitiveOperand
    {
        public ushort Data;
        public VMSlotScope Type;
        public byte Flags;

        #region VMPrimitiveOperand Members
        public void Read(byte[] bytes){
            using (var io = IoBuffer.FromBytes(bytes, ByteOrder.LITTLE_ENDIAN)){
                Data = io.ReadUInt16();
                Type = (VMSlotScope)io.ReadUInt16();
                Flags = io.ReadByte();
            }
        }
        #endregion

        public override string ToString()
        {
            return "Go To Routing Slot (" + Type.ToString() + ": #" + Data + ")";
        }
    }

}
