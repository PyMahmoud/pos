using System;
using System.Collections.Generic;

namespace PosSystem.Core.Models
{
    public enum BusinessType { FoodTruck, Cafe, Restaurant, Retail }
    public enum EmployeeRole { Owner, Manager, Cashier }
    public enum OrderStatus { Completed, Voided, Refunded }
    public enum PaymentMethod { Cash, Card }

    public class Business
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public BusinessType BusinessType { get; set; }
        public string? OwnerName { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class Employee
    {
        public int Id { get; set; }
        public int BusinessId { get; set; }
        public string Name { get; set; } = string.Empty;
        public EmployeeRole Role { get; set; }
        public string PinCode { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
    }

    public class Category
    {
        public int Id { get; set; }
        public int BusinessId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int SortOrder { get; set; }
    }

    public class Product
    {
        public int Id { get; set; }
        public int BusinessId { get; set; }
        public int? CategoryId { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public bool IsAvailable { get; set; } = true; // sold-out toggle
        public string? SKU { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<Modifier> Modifiers { get; set; } = new();
    }

    public class Modifier
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal PriceDelta { get; set; }
    }

    public class Order
    {
        public int Id { get; set; }
        public int BusinessId { get; set; }
        public int? EmployeeId { get; set; }
        public DateTime CreatedAt { get; set; }
        public OrderStatus Status { get; set; } = OrderStatus.Completed;
        public decimal TotalAmount { get; set; }
        public PaymentMethod PaymentMethod { get; set; }
        public DateTime? SyncedAt { get; set; } // null = not yet pushed to central server
        public List<OrderItem> Items { get; set; } = new();
    }

    public class OrderItem
    {
        public int Id { get; set; }
        public int OrderId { get; set; }
        public int ProductId { get; set; }
        public int Quantity { get; set; } = 1;
        public decimal UnitPrice { get; set; } // snapshot at sale time
        public string? Notes { get; set; }
    }
}
