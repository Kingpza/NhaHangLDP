using System;
using System.Collections.Generic;

namespace NhaHangLDP.Models;

public partial class MenuItem
{
    public int Id { get; set; }

    public string Name { get; set; }

    public string Description { get; set; }

    public decimal Price { get; set; }

    public decimal OriginalPrice { get; set; }

    public string Category { get; set; }

    public string ImageUrl { get; set; }

    public int PreparationTime { get; set; }

    public bool IsAvailable { get; set; }

    public bool IsFeatured { get; set; }

    public bool IsNew { get; set; }

    public decimal Rating { get; set; }

    public int ReviewCount { get; set; }

    public int SoldCount { get; set; }

    public DateTime CreatedDate { get; set; }

    public DateTime? UpdatedDate { get; set; }

    public string CreatedBy { get; set; }

    public virtual ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();

    public virtual ICollection<CustomerOrderDetail> CustomerOrderDetails { get; set; } = new List<CustomerOrderDetail>();

    public virtual ICollection<KitchenOrderItem> KitchenOrderItems { get; set; } = new List<KitchenOrderItem>();

    public virtual ICollection<MenuComboItem> MenuComboItems { get; set; } = new List<MenuComboItem>();

    public virtual ICollection<MenuItemIngredient> MenuItemIngredients { get; set; } = new List<MenuItemIngredient>();

    public virtual ICollection<OrderDetail> OrderDetails { get; set; } = new List<OrderDetail>();

    public virtual ICollection<PriceHistory> PriceHistories { get; set; } = new List<PriceHistory>();

    public virtual ICollection<QROrderDetail> QROrderDetails { get; set; } = new List<QROrderDetail>();

    public virtual ICollection<ReturnBillDetail> ReturnBillDetails { get; set; } = new List<ReturnBillDetail>();

    public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();

    public virtual ICollection<Wishlist> Wishlists { get; set; } = new List<Wishlist>();
}
