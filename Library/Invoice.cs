﻿using System;
using System.Net;
using System.Xml;
using System.Collections.Generic;

namespace Recurly
{
    public class Invoice : RecurlyEntity
    {
        // The currently valid Invoice States
        public enum InvoiceState
        {
            Open,
            Collected,
            Failed,
            PastDue
        }

        public string AccountCode { get; private set; }
        public string SubscriptionUuid { get; private set; }
        public string Uuid { get; protected set; }
        public InvoiceState State { get; protected set; }
        public int InvoiceNumber { get; private set; }
        public string PoNumber { get; private set; }
        public string VatNumber { get; private set; }
        public int SubtotalInCents { get; private set; }
        public int TaxInCents { get; protected set; }
        public int TotalInCents { get; protected set; }
        public string Currency { get; protected set; }
        public DateTime? CreatedAt { get; private set; }
        public DateTime? ClosedAt { get; private set; }

        /// <summary>
        /// Tax type as "vat" for VAT or "usst" for US Sales Tax.
        /// </summary>
        public string TaxType { get; private set; }

        public decimal? TaxRate { get; private set; }

        public int? NetTerms { get; private set; }

        public string CollectionMethod { get; private set; }


        public RecurlyList<Adjustment> Adjustments { get; private set; }
        public RecurlyList<Transaction> Transactions { get; private set; }


        internal const string UrlPrefix = "/invoices/";

        internal Invoice()
        {
            Adjustments = new AdjustmentList();
            Transactions = new TransactionList();
        }

        internal Invoice(XmlTextReader reader)
            : this()
        {
            ReadXml(reader);
        }

        /// <summary>
        /// Returns a PDF representation of an invoice
        /// </summary>
        /// <param name="acceptLanguage">Language for invoice, defaults to en-US.</param>
        /// <returns></returns>
        public byte[] GetPdf(string acceptLanguage = "en-US")
        {
            return Client.Instance.PerformDownloadRequest(UrlPrefix + InvoiceNumber,
               "application/pdf", acceptLanguage);
        }

        /// <summary>
        /// Marks an invoice as paid successfully
        /// </summary>
        public void MarkSuccessful()
        {
            Client.Instance.PerformRequest(Client.HttpRequestMethod.Put,
                UrlPrefix + InvoiceNumber + "/mark_successful",
                ReadXml);

        }

        /// <summary>
        /// Marks an invoice as failed collection
        /// </summary>
        public void MarkFailed()
        {
            Client.Instance.PerformRequest(Client.HttpRequestMethod.Put,
               UrlPrefix + InvoiceNumber + "/mark_failed",
               ReadXml);

        }

        /// <summary>
        /// Returns the active coupon redemption on this invoice
        /// </summary>
        /// <returns></returns>
        public CouponRedemption GetRedemption()
        {
            var cr = new CouponRedemption();

            var statusCode = Client.Instance.PerformRequest(Client.HttpRequestMethod.Get,
                UrlPrefix + InvoiceNumber + "/redemption",
                cr.ReadXml);

            return statusCode == HttpStatusCode.NotFound ? null : cr;
        }

        /// <summary>
        /// If enabled, allows specific line items and/or quantities to be refunded.
        /// </summary>
        /// <param name="adjustment"></param>
        /// <param name="prorate"></param>
        /// <param name="quantity"></param>
        /// <returns>new Invoice object</returns>
        public Invoice Refund(Adjustment adjustment, bool prorate = false, int quantity = 0)
        {
            var adjustments = new List<Adjustment>();
            adjustments.Add(adjustment);

            return Refund(adjustments, prorate, quantity);
        }

        public Invoice Refund(IEnumerable<Adjustment> adjustments, bool prorate = false, int quantity = 0)
        {
            var refunds = new RefundList(adjustments, prorate, quantity);
            var invoice = new Invoice();

            var response = Client.Instance.PerformRequest(Client.HttpRequestMethod.Post,
                UrlPrefix + InvoiceNumber + "/refund",
                refunds.WriteXml,
                invoice.ReadXml);

            if (HttpStatusCode.Created == response || HttpStatusCode.OK == response)
                return invoice;
            else
                return null;
        }

        #region Read and Write XML documents

        internal override void ReadXml(XmlTextReader reader)
        {
            while (reader.Read())
            {
                // End of invoice element, get out of here
                if (reader.Name == "invoice" && reader.NodeType == XmlNodeType.EndElement)
                    break;

                if (reader.NodeType != XmlNodeType.Element) continue;

                switch (reader.Name)
                {
                    case "account":
                        var accountHref = reader.GetAttribute("href");
                        AccountCode = Uri.UnescapeDataString(accountHref.Substring(accountHref.LastIndexOf("/") + 1));
                        break;

                    case "subscription":
                        var subHref = reader.GetAttribute("href");
                        SubscriptionUuid = Uri.UnescapeDataString(subHref.Substring(subHref.LastIndexOf("/") + 1));
                        break;

                    case "uuid":
                        Uuid = reader.ReadElementContentAsString();
                        break;

                    case "state":
                        State = reader.ReadElementContentAsString().ParseAsEnum<InvoiceState>();
                        break;

                    case "invoice_number":
                        int invNumber;
                        if (Int32.TryParse(reader.ReadElementContentAsString(), out invNumber))
                            InvoiceNumber = invNumber;
                        break;

                    case "po_number":
                        PoNumber = reader.ReadElementContentAsString();
                        break;

                    case "vat_number":
                        VatNumber = reader.ReadElementContentAsString();
                        break;

                    case "subtotal_in_cents":
                        SubtotalInCents = reader.ReadElementContentAsInt();
                        break;

                    case "tax_in_cents":
                        TaxInCents = reader.ReadElementContentAsInt();
                        break;

                    case "total_in_cents":
                        TotalInCents = reader.ReadElementContentAsInt();
                        break;

                    case "currency":
                        Currency = reader.ReadElementContentAsString();
                        break;

                    case "created_at":
                        DateTime createdAt;
                        if (DateTime.TryParse(reader.ReadElementContentAsString(), out createdAt))
                            CreatedAt = createdAt;
                        break;

                    case "closed_at":
                        DateTime closedAt;
                        if (DateTime.TryParse(reader.ReadElementContentAsString(), out closedAt))
                            ClosedAt = closedAt;
                        break;

                    case "tax_type":
                        TaxType = reader.ReadElementContentAsString();
                        break;

                    case "tax_rate":
                        TaxRate = reader.ReadElementContentAsDecimal();
                        break;

                    case "net_terms":
                        NetTerms = reader.ReadElementContentAsInt();
                        break;

                    case "collection_method":
                        CollectionMethod = reader.ReadElementContentAsString();
                        break;

                    case "line_items":
                        // overrite existing value with the Recurly API response
                        Adjustments = new AdjustmentList();
                        Adjustments.ReadXml(reader);
                        break;

                    case "transactions":
                        // overrite existing value with the Recurly API response
                        Transactions = new TransactionList();
                        Transactions.ReadXml(reader);
                        break;
                }
            }
        }

        internal override void WriteXml(XmlTextWriter writer)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Object Overrides

        public override string ToString()
        {
            return "Recurly Invoice: " + Uuid;
        }

        public override bool Equals(object obj)
        {
            var invoice = obj as Invoice;
            return invoice != null && Equals(invoice);
        }

        public bool Equals(Invoice invoice)
        {
            return Uuid == invoice.Uuid;
        }

        public override int GetHashCode()
        {
            return Uuid.GetHashCode();
        }

        #endregion
    }

    public sealed class Invoices
    {
        public static RecurlyList<Invoice> List(string accountCode)
        {
            return new InvoiceList("/accounts/" + Uri.EscapeUriString(accountCode) + "/invoices");
        }

        public static RecurlyList<Invoice> List()
        {
            return new InvoiceList(Invoice.UrlPrefix);
        }

        public static RecurlyList<Invoice> List(Invoice.InvoiceState state)
        {
            return new InvoiceList(Invoice.UrlPrefix + "?state=" + state.ToString().EnumNameToTransportCase());
        }

        /// <summary>
        /// Look up an Invoice.
        /// </summary>
        /// <param name="invoiceNumber">Invoice Number</param>
        /// <returns></returns>
        public static Invoice Get(int invoiceNumber)
        {
            var invoice = new Invoice();

            var statusCode = Client.Instance.PerformRequest(Client.HttpRequestMethod.Get,
                Invoice.UrlPrefix + invoiceNumber,
                invoice.ReadXml);

            return statusCode == HttpStatusCode.NotFound ? null : invoice;
        }

        /// <summary>
        /// Create an Invoice if there are outstanding charges on an account. If there are no outstanding
        /// charges, null is returned.
        /// </summary>
        /// <param name="accountCode">Account code</param>
        /// <returns></returns>
        public static Invoice Create(string accountCode)
        {
            var invoice = new Invoice();

            var statusCode = Client.Instance.PerformRequest(Client.HttpRequestMethod.Post,
                "/accounts/" + Uri.EscapeUriString(accountCode) + Invoice.UrlPrefix,
                invoice.ReadXml);

            return (int)statusCode == ValidationException.HttpStatusCode ? null : invoice;
        }
    }
}
