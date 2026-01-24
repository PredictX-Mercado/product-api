namespace Product.Contracts.Wallet;

public class ReceiptListResponse
{
    public List<ReceiptItem> Items { get; set; } = new List<ReceiptItem>();
    public string? NextCursor { get; set; }
}
