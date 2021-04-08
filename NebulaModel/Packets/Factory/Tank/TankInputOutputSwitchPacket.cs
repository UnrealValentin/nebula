﻿namespace NebulaModel.Packets.Factory.Tank
{
    public class TankInputOutputSwitchPacket
    {
        public int TankIndex { get; set; }
        public bool IsInput { get; set; }
        public bool IsClosed { get; set; }

        public TankInputOutputSwitchPacket() { }

        public TankInputOutputSwitchPacket(int tankIndex, bool isInput, bool inClosed)
        {
            TankIndex = tankIndex;
            IsInput = isInput;
            IsClosed = inClosed;
        }
    }
}
