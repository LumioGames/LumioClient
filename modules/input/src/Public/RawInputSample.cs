namespace Lumio.Client.Input
{
    public readonly struct RawInputSample
    {
        public RawInputSample(uint buttons, short axisX, short axisY)
        {
            Buttons = buttons;
            AxisX = axisX;
            AxisY = axisY;
        }

        public uint Buttons { get; }

        public short AxisX { get; }

        public short AxisY { get; }
    }
}
