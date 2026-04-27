namespace AlbionMarketCollector.Infrastructure.Protocol;

internal sealed class PhotonFragmentAssembler
{
    private readonly Dictionary<int, FragmentAccumulator> _pendingSegments = [];

    public byte[]? AddFragment(
        int startSequence,
        int fragmentCount,
        int fragmentNumber,
        int totalLength,
        int fragmentOffset,
        ReadOnlySpan<byte> payload)
    {
        if (startSequence < 0 ||
            fragmentCount <= 0 ||
            fragmentNumber < 0 ||
            fragmentNumber >= fragmentCount ||
            totalLength <= 0 ||
            fragmentOffset < 0 ||
            fragmentOffset + payload.Length > totalLength)
        {
            return null;
        }

        if (!_pendingSegments.TryGetValue(startSequence, out var accumulator))
        {
            accumulator = new FragmentAccumulator(totalLength, fragmentCount);
            _pendingSegments[startSequence] = accumulator;
        }

        if (accumulator.TotalLength != totalLength || accumulator.FragmentCount != fragmentCount)
        {
            _pendingSegments.Remove(startSequence);
            return null;
        }

        if (!accumulator.MarkReceived(fragmentNumber, payload.Length))
        {
            return null;
        }

        payload.CopyTo(accumulator.Buffer.AsSpan(fragmentOffset, payload.Length));
        if (!accumulator.IsComplete)
        {
            return null;
        }

        _pendingSegments.Remove(startSequence);
        return accumulator.Buffer;
    }

    private sealed class FragmentAccumulator
    {
        private readonly bool[] _receivedFragments;
        private int _bytesReceived;

        public FragmentAccumulator(int totalLength, int fragmentCount)
        {
            TotalLength = totalLength;
            FragmentCount = fragmentCount;
            Buffer = new byte[totalLength];
            _receivedFragments = new bool[fragmentCount];
        }

        public int TotalLength { get; }

        public int FragmentCount { get; }

        public byte[] Buffer { get; }

        public bool IsComplete => _bytesReceived >= TotalLength;

        public bool MarkReceived(int fragmentNumber, int payloadLength)
        {
            if (_receivedFragments[fragmentNumber])
            {
                return false;
            }

            _receivedFragments[fragmentNumber] = true;
            _bytesReceived += payloadLength;
            return true;
        }
    }
}
