namespace software_elviejomadero.Models
{
    public enum OrderType
    {
        Mesa = 1,
        ParaLlevar = 2,
        Delivery = 3
    }

    /// <summary>
    /// Flujo del pedido en cocina (HU-09): Nuevo -> EnPreparacion -> Listo.
    /// "Entregado" queda reservado para la historia de entrega (mozo / recepcionista / repartidor);
    /// el tablero de cocina nunca muestra pedidos entregados.
    /// </summary>
    public enum OrderStatus
    {
        Nuevo = 1,
        EnPreparacion = 2,
        Listo = 3,
        Entregado = 4
    }
}