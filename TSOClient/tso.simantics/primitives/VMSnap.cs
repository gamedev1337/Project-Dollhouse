﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TSO.Simantics.engine;
using TSO.Files.utils;
using TSO.Simantics.engine.scopes;
using TSO.Simantics.engine.utils;
using Microsoft.Xna.Framework;
using tso.world.model;
using TSO.Files.formats.iff.chunks;
using TSO.Simantics.model;
using TSO.Common.utils;

namespace TSO.Simantics.primitives
{
    public class VMSnap : VMPrimitiveHandler 
    {
        public override VMPrimitiveExitCode Execute(VMStackFrame context)
        {
            var operand = context.GetCurrentOperand<VMSnapOperand>();
            var avatar = context.Caller; //todo, can sometimes be an object?? see roaches object tile movement, snaps to its own routing slot
            var obj = context.StackObject;

            var prevContain = avatar.Container;
            if (prevContain != null) //if we are contained in an object, drop out of it.
            {
                prevContain.ClearSlot(avatar.ContainerSlot);
            }

            SLOTItem slot = null;
            List<VMFindLocationResult> locations = null;

            switch (operand.Mode)
            {
                case 0:
                    slot = VMMemory.GetSlot(context, VMSlotScope.StackVariable, operand.Index);
                    locations = VMSlotParser.FindAvaliableLocations(obj, slot, context.VM.Context);
                    break;
                case 1: //be contained on stack object
                    context.StackObject.PlaceInSlot(context.Caller, 0);
                break;
                case 2:
                    var pos = obj.Position;
                    switch (obj.Direction)
                    {
                        case tso.world.model.Direction.SOUTH:
                            pos.y += 16;
                            break;
                        case tso.world.model.Direction.WEST:
                            pos.x -= 16;
                            break;
                        case tso.world.model.Direction.EAST:
                            pos.x += 16;
                            break;
                        case tso.world.model.Direction.NORTH:
                            pos.y -= 16;
                            break;
                    }

                    SetPosition(avatar, pos, obj.RadianDirection, context.VM.Context);
                break;
                case 3:
                    slot = VMMemory.GetSlot(context, VMSlotScope.Literal, operand.Index);
                    locations = VMSlotParser.FindAvaliableLocations(obj, slot, context.VM.Context); //chair seems to snap to position?
                    break;
                case 4:
                    slot = VMMemory.GetSlot(context, VMSlotScope.Global, operand.Index);
                    locations = VMSlotParser.FindAvaliableLocations(obj, slot, context.VM.Context);
                    break;
            }

            if (operand.Mode != 1 && operand.Mode != 2)
            {
                if (slot.SnapTargetSlot != -1)
                {
                    context.StackObject.PlaceInSlot(context.Caller, slot.SnapTargetSlot);
                    if (locations.Count > 0) avatar.RadianDirection = ((slot.Rsflags & SLOTFlags.SnapToDirection) > 0) ? locations[0].RadianDirection: avatar.RadianDirection;
                }
                else
                {
                    if (locations.Count > 0)
                    {
                        if (!SetPosition(avatar, locations[0].Position,
                            ((slot.Rsflags & SLOTFlags.SnapToDirection) > 0) ? locations[0].RadianDirection : avatar.RadianDirection,
                            context.VM.Context))
                            return VMPrimitiveExitCode.GOTO_FALSE;
                    }
                }
            }

            return VMPrimitiveExitCode.GOTO_TRUE; 
        }

        private bool SetPosition(VMEntity entity, LotTilePos pos, Direction dir, VMContext context)
        {
            return SetPosition(entity, pos, (float)(Math.Round(Math.Log((double)dir, 2))*(Math.PI/4)), context);
        }

        private bool SetPosition(VMEntity entity, LotTilePos pos, float radDir, VMContext context)
        {
            if (entity is VMGameObject)
            {
                var posChange = entity.SetPosition(pos, (Direction)(1 << (int)(Math.Round(DirectionUtils.PosMod(radDir, (float)Math.PI * 2) / (Math.PI/4)) % 8)), context);
                if (posChange != VMPlacementError.Success) return false;
            }
            else
            {
                entity.Position = pos;
                entity.RadianDirection = radDir;
            }
            return true;
        }
    }

    public class VMSnapOperand : VMPrimitiveOperand
    {
        public ushort Index;
        public ushort Mode;
        public byte Flags;

        #region VMPrimitiveOperand Members
        public void Read(byte[] bytes)
        {
            using (var io = IoBuffer.FromBytes(bytes, ByteOrder.LITTLE_ENDIAN))
            {
                Index = io.ReadUInt16();
                Mode = io.ReadUInt16();
                Flags = io.ReadByte(); 
            }
        }
        #endregion
    }
}
