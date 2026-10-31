namespace software_elviejomadero.Services.Implementations
{
    /// <summary>
    /// HU-05: estimación conservadora por preparación secuencial.
    /// Se suman (minutos de preparación de cada plato × cantidad).
    /// Si el curso define preparación paralela, aquí se modifica la regla.
    /// </summary>
    public static class SalonAttentionEstimator
    {
        public static int CalculateMinutes(IEnumerable<(int MinutesPerDish, int Quantity)> items)
        {
            long minutes = 0;
            foreach (var (prep, quantity) in items)
            {
                if (prep < 0 || quantity < 0)
                    throw new ArgumentOutOfRangeException(nameof(items), "Tiempo y cantidad no pueden ser negativos.");
                minutes += (long)prep * quantity;
                if (minutes > int.MaxValue) return int.MaxValue;
            }
            return (int)minutes;
        }
    }
}
