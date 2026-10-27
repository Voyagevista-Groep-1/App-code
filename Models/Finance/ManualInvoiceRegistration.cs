using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace tui.Models;

public class ManualInvoiceRegistration
{
    [Key] public int InvoiceId { get; set; }
    [MaxLength(100)] public string OriginalInvoiceNumber { get; set; } = "";
    [MaxLength(64)] public string SourceIdentityKey { get; set; } = "";
    [MaxLength(150)] public string SenderName { get; set; } = "";
    [MaxLength(500)] public string SenderAddress { get; set; } = "";
    [MaxLength(30)] public string? SenderKvkNumber { get; set; }
    [MaxLength(50)] public string? SenderVatNumber { get; set; }
    [MaxLength(100)] public string RecipientName { get; set; } = "";
    [MaxLength(255)] public string? RecipientAddress { get; set; }
    [MaxLength(50)] public string? RecipientVatNumber { get; set; }
    public DateOnly? DeliveryDate { get; set; }
    public DateOnly? DeliveryPeriodStart { get; set; }
    public DateOnly? DeliveryPeriodEnd { get; set; }
    [MaxLength(255)] public string? Subject { get; set; }
    [MaxLength(100)] public string? PaymentReference { get; set; }
    public int? PaymentTermDays { get; set; }
    [MaxLength(4000)] public string? Notes { get; set; }
    [Precision(14, 4)] public decimal SubtotalBeforeDiscount { get; set; }
    [Precision(14, 4)] public decimal TotalDiscount { get; set; }
    [Precision(14, 4)] public decimal TotalExcludingVat { get; set; }
    [Precision(14, 4)] public decimal TotalVat { get; set; }
    [Precision(14, 4)] public decimal? SourceTotalAmount { get; set; }
    [MaxLength(50)] public string CalculationPolicy { get; set; } = "per-line-away-from-zero-v1";
    public Invoice Invoice { get; set; } = null!;
}
