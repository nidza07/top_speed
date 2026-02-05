using System;
using System.Collections.Generic;

namespace TopSpeed.Tracks.Surfaces
{
    public sealed class TrackBankDefinition
    {
        private static readonly IReadOnlyDictionary<string, string> EmptyParameters =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public TrackBankDefinition(
            string id,
            TrackBankType type,
            TrackBankSide side,
            string? name,
            IReadOnlyDictionary<string, string>? parameters = null)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Bank id is required.", nameof(id));

            Id = id.Trim();
            Type = type;
            Side = side;
            var trimmedName = name?.Trim();
            Name = string.IsNullOrWhiteSpace(trimmedName) ? null : trimmedName;
            Parameters = Normalize(parameters);
        }

        public string Id { get; }
        public TrackBankType Type { get; }
        public TrackBankSide Side { get; }
        public string? Name { get; }
        public IReadOnlyDictionary<string, string> Parameters { get; }

        private static IReadOnlyDictionary<string, string> Normalize(IReadOnlyDictionary<string, string>? parameters)
        {
            if (parameters == null || parameters.Count == 0)
                return EmptyParameters;
            var copy = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in parameters)
                copy[pair.Key] = pair.Value;
            return copy;
        }
    }
}
