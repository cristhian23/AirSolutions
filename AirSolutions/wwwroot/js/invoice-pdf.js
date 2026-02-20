(function () {
  const BLUE = [27, 94, 169];
  const LIGHT_GRAY = [245, 245, 245];
  const BORDER_GRAY = [211, 211, 211];

  function parseNumber(value, fallback) {
    const parsed = parseFloat(value);
    return Number.isNaN(parsed) ? (fallback ?? 0) : parsed;
  }

  function money(value) {
    return 'RD$' + parseNumber(value, 0).toLocaleString('en-US', {
      minimumFractionDigits: 2,
      maximumFractionDigits: 2
    });
  }

  function formatDate(value) {
    if (!value) return '-';
    const dt = new Date(value);
    if (Number.isNaN(dt.getTime())) return '-';

    const day = String(dt.getDate()).padStart(2, '0');
    const month = String(dt.getMonth() + 1).padStart(2, '0');
    const year = dt.getFullYear();
    return day + '/' + month + '/' + year;
  }

  function formatQty(value) {
    const qty = parseNumber(value, 0);
    if (Math.abs(qty - Math.round(qty)) < 0.000001) return String(Math.round(qty));
    return qty.toFixed(2);
  }

  function clientName(client) {
    if (!client) return '-';
    if (client.clientType === 'Company') return client.companyName || '-';
    const fullName = ((client.firstName || '') + ' ' + (client.lastName || '')).trim();
    return fullName || '-';
  }

  function cleanText(value) {
    return (value == null ? '' : String(value)).trim();
  }

  async function loadLogoDataUrl() {
    const logoCandidates = ['/logo.png', '/logo.png.png'];
    for (const path of logoCandidates) {
      try {
        const response = await fetch(path);
        if (!response.ok) continue;
        const blob = await response.blob();
        const dataUrl = await new Promise((resolve, reject) => {
          const reader = new FileReader();
          reader.onload = function () { resolve(reader.result); };
          reader.onerror = reject;
          reader.readAsDataURL(blob);
        });
        return dataUrl;
      } catch {
        // Try next candidate.
      }
    }
    return null;
  }

  function truncateText(doc, text, maxWidth) {
    const clean = cleanText(text || '-');
    if (!clean) return '-';
    const ellipsis = '...';
    if (doc.getTextWidth(clean) <= maxWidth) return clean;

    let output = clean;
    while (output.length > 0 && doc.getTextWidth(output + ellipsis) > maxWidth) {
      output = output.slice(0, -1);
    }
    return (output || clean).trim() + ellipsis;
  }

  function drawHeader(doc, invoice, logoDataUrl) {
    const pageWidth = doc.internal.pageSize.getWidth();
    const centerX = pageWidth / 2;

    if (logoDataUrl) {
      try {
        doc.addImage(logoDataUrl, 'PNG', (pageWidth - 96) / 2, 26, 96, 96);
      } catch {
        // Continue even if logo cannot be rendered.
      }
    }

    doc.setTextColor(BLUE[0], BLUE[1], BLUE[2]);
    doc.setFont('helvetica', 'bold');
    doc.setFontSize(13);
    doc.text('Air Solutions, R.D.', centerX, 132, { align: 'center' });

    doc.setTextColor(0, 0, 0);
    doc.setFont('helvetica', 'normal');
    doc.setFontSize(10);
    doc.text('Mas que aires, soluciones', centerX, 148, { align: 'center' });
    doc.text('809-657-3428 | airsolutionsrd@gmail.com', centerX, 162, { align: 'center' });

    doc.setTextColor(BLUE[0], BLUE[1], BLUE[2]);
    doc.setFont('helvetica', 'bold');
    doc.setFontSize(24);
    doc.text('FACTURA', centerX, 195, { align: 'center' });

    doc.setDrawColor(BORDER_GRAY[0], BORDER_GRAY[1], BORDER_GRAY[2]);
    doc.setLineWidth(0.8);
    doc.line(46, 210, pageWidth - 46, 210);
  }

  function drawInvoiceInfo(doc, invoice) {
    const pageWidth = doc.internal.pageSize.getWidth();
    const leftX = 46;
    const rightX = pageWidth / 2 + 10;
    const labelWidth = 92;
    const valueWidth = 140;
    const startY = 236;
    const rowGap = 20;

    const rows = [
      ['Factura #', invoice.invoiceCode || ('FACTURA-' + (invoice.id || '-'))],
      ['Cliente', clientName(invoice.client)],
      ['Comprobante', (invoice.fiscalVoucher && invoice.fiscalVoucher.voucherNumber) ? invoice.fiscalVoucher.voucherNumber : 'Sin comprobante'],
      ['Vendedor', 'Cristhian Cuevas'],
      ['Contacto', '809-657-3428'],
      ['Correo', 'airsolutionsrd@gmail.com'],
      ['Fecha emisión', formatDate(invoice.issueDate || invoice.createdAt)],
      ['Fecha venc.', formatDate(invoice.dueDate)]
    ];

    rows.forEach(function (row, index) {
      const y = startY + (Math.floor(index / 2) * rowGap);
      const baseX = index % 2 === 0 ? leftX : rightX;
      doc.setFont('helvetica', 'bold');
      doc.setFontSize(10);
      doc.setTextColor(BLUE[0], BLUE[1], BLUE[2]);
      doc.text(row[0] + ':', baseX, y);

      doc.setFont('helvetica', 'normal');
      doc.setTextColor(0, 0, 0);
      const value = truncateText(doc, row[1], valueWidth);
      doc.text(value, baseX + labelWidth, y);
    });

    const endY = startY + (Math.ceil(rows.length / 2) * rowGap) - 8;
    doc.setDrawColor(BORDER_GRAY[0], BORDER_GRAY[1], BORDER_GRAY[2]);
    doc.setLineWidth(0.6);
    doc.line(46, endY, pageWidth - 46, endY);
    return endY + 24;
  }

  function drawInvoiceTable(doc, invoice, startY) {
    const x = 47.64;
    const y = startY;
    const tableWidth = 500;
    const colWidths = [70, 230, 100, 100];
    const headerH = 24;
    const rowH = 22;
    const minLines = 4;

    const lines = (invoice.lines || []).map(function (line) {
      return {
        qty: formatQty(line.quantity),
        description: cleanText(line.name || line.description || '-'),
        unitPrice: money(line.unitPrice),
        total: money(line.lineTotal)
      };
    });

    while (lines.length < minLines) {
      lines.push({ qty: '', description: '', unitPrice: '', total: '' });
    }

    const subtotal = parseNumber(invoice.grandTotal, 0);
    const tableH = headerH + (lines.length * rowH) + rowH;

    doc.setFillColor(BLUE[0], BLUE[1], BLUE[2]);
    doc.rect(x, y, tableWidth, headerH, 'F');

    doc.setFillColor(LIGHT_GRAY[0], LIGHT_GRAY[1], LIGHT_GRAY[2]);
    doc.rect(x + colWidths[0] + colWidths[1], y + headerH + (lines.length * rowH), colWidths[2] + colWidths[3], rowH, 'F');

    doc.setTextColor(255, 255, 255);
    doc.setFont('helvetica', 'bold');
    doc.setFontSize(10.5);
    doc.text('Cantidad', x + 6, y + 15);
    doc.text('Descripción', x + colWidths[0] + 6, y + 15);
    doc.text('Precio Unitario', x + colWidths[0] + colWidths[1] + 6, y + 15);
    doc.text('Total', x + colWidths[0] + colWidths[1] + colWidths[2] + 6, y + 15);

    doc.setTextColor(0, 0, 0);
    doc.setFont('helvetica', 'normal');
    doc.setFontSize(10);

    lines.forEach(function (line, index) {
      const rowY = y + headerH + (index * rowH);
      doc.text(line.qty, x + 6, rowY + 11);

      const descX = x + colWidths[0] + 6;
      const descMaxWidth = colWidths[1] - 12;
      const wrapped = doc.splitTextToSize(line.description || '', descMaxWidth);
      if (wrapped.length > 0) {
        doc.text(wrapped[0], descX, rowY + 14);
      }

      doc.text(line.unitPrice, x + colWidths[0] + colWidths[1] + colWidths[2] - 6, rowY + 14, { align: 'right' });
      doc.text(line.total, x + tableWidth - 6, rowY + 14, { align: 'right' });
    });

    const totalY = y + headerH + (lines.length * rowH);
    doc.setFont('helvetica', 'bold');
    doc.setFontSize(10);
    doc.text('Total General', x + colWidths[0] + colWidths[1] + 30, totalY + 14);
    doc.text(money(subtotal), x + tableWidth - 6, totalY + 14, { align: 'right' });

    doc.setDrawColor(BORDER_GRAY[0], BORDER_GRAY[1], BORDER_GRAY[2]);
    doc.setLineWidth(0.5);

    const verticals = [
      x,
      x + colWidths[0],
      x + colWidths[0] + colWidths[1],
      x + colWidths[0] + colWidths[1] + colWidths[2],
      x + tableWidth
    ];

    verticals.forEach(function (vx) { doc.line(vx, y, vx, y + tableH); });

    doc.line(x, y, x + tableWidth, y);
    doc.line(x, y + headerH, x + tableWidth, y + headerH);

    for (let i = 1; i <= lines.length; i++) {
      const rowLineY = y + headerH + (i * rowH);
      doc.line(x, rowLineY, x + tableWidth, rowLineY);
    }

    doc.line(x, y + tableH, x + tableWidth, y + tableH);
    return y + tableH;
  }

  function drawPriceBreakdown(doc, invoice, startY) {
    const x = 320;
    const labelX = x + 12;
    const valueX = x + 212;
    const width = 230;
    const rowH = 22;

    const subtotal = parseNumber(invoice.subtotal, 0);
    const discount = parseNumber(invoice.discountTotal, 0);
    const tax = parseNumber(invoice.taxTotal, 0);
    const netTotal = parseNumber(invoice.grandTotal, 0);

    doc.setDrawColor(BORDER_GRAY[0], BORDER_GRAY[1], BORDER_GRAY[2]);
    doc.setLineWidth(0.5);
    doc.rect(x, startY, width, rowH * 5.2);

    doc.setFont('helvetica', 'bold');
    doc.setFontSize(11);
    doc.setTextColor(BLUE[0], BLUE[1], BLUE[2]);
    doc.text('Detalle de precio', labelX, startY + 14);

    const rows = [
      ['Subtotal:', money(subtotal)],
      ['Descuento:', '- ' + money(discount)],
      ['ITBIS agregado:', '+ ' + money(tax)],
      ['Total neto:', money(netTotal)]
    ];

    doc.setTextColor(0, 0, 0);
    doc.setFont('helvetica', 'normal');
    doc.setFontSize(10);

    rows.forEach(function (row, index) {
      const y = startY + rowH * (index + 1);
      doc.line(x, y, x + width, y);
      if (index === rows.length - 1) {
        doc.setFont('helvetica', 'bold');
      } else {
        doc.setFont('helvetica', 'normal');
      }
      doc.text(row[0], labelX, y + 14);
      doc.text(row[1], valueX, y + 14, { align: 'right' });
    });
  }

  async function generateInvoicePdf(invoice, options) {
    if (!window.jspdf || !window.jspdf.jsPDF) {
      throw new Error('jsPDF no esta disponible.');
    }

    const jsPDF = window.jspdf.jsPDF;
    const doc = new jsPDF({ orientation: 'portrait', unit: 'pt', format: 'a4' });

    const logoDataUrl = await loadLogoDataUrl();
    drawHeader(doc, invoice, logoDataUrl);
    const tableStartY = drawInvoiceInfo(doc, invoice);
    const tableBottom = drawInvoiceTable(doc, invoice, tableStartY);
    drawPriceBreakdown(doc, invoice, tableBottom + 24);

    const code = cleanText(invoice.invoiceCode || ('FACTURA-' + (invoice.id || 'sin_id')))
      .replace(/[^a-z0-9_-]+/gi, '_')
      .toLowerCase();
    const fileName = (options && options.fileName) || ('factura_' + code + '.pdf');

    doc.save(fileName);
  }

  window.InvoicePdf = {
    generateInvoicePdf: generateInvoicePdf
  };
})();
