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
          reader.onload = () => resolve(reader.result);
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

  function drawHeader(doc, quote, logoDataUrl) {
    const pageWidth = doc.internal.pageSize.getWidth();

    if (logoDataUrl) {
      try {
        // Posicion y tamano tomados del PDF de referencia.
        doc.addImage(logoDataUrl, 'PNG', (pageWidth - 120) / 2, 46, 120, 120);
      } catch {
        // Continue even if logo cannot be rendered.
      }
    }

    doc.setTextColor(BLUE[0], BLUE[1], BLUE[2]);
    doc.setFont('helvetica', 'normal');
    doc.setFontSize(18);
    doc.text('COTIZACIÓN', 46, 196);

    doc.setFont('helvetica', 'bold');
    doc.setFontSize(11);
    doc.text('Air Solutions, R.D.', 46, 225);

    doc.setTextColor(0, 0, 0);
    doc.setFont('helvetica', 'normal');
    doc.setFontSize(10);
    doc.text('Mas que aires, soluciones', 46, 244);

    const infoYStart = 271;
    const infoGap = 14;

    const infoRows = [
      ['Cliente:', clientName(quote.client)],
      ['Vendedor:', ' Cristhian Cuevas'],
      ['Contacto:', '809-657-3428'],
      ['Correo:', 'airsolutionsrd@gmail.com'],
      ['Fecha:', formatDate(quote.createdAt)]
    ];

    infoRows.forEach((row, index) => {
      const y = infoYStart + (index * infoGap);
      doc.setFont('helvetica', 'bold');
      doc.text(row[0], 46, y);
      doc.setFont('helvetica', 'normal');
      doc.text(' ' + (cleanText(row[1]) || '-'), 92, y);
    });
  }

  function drawQuoteTable(doc, quote) {
    const x = 47.64;
    const y = 350;
    const tableWidth = 500;
    const colWidths = [70, 230, 100, 100];
    const headerH = 18;
    const rowH = 16.8;
    const minLines = 4;

    const lines = (quote.lines || []).map(line => {
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

    const visibleLines = lines;

    const subtotal = (quote.lines || []).reduce((sum, line) => sum + parseNumber(line.lineTotal, 0), 0);

    const tableH = headerH + (visibleLines.length * rowH) + rowH;

    doc.setFillColor(BLUE[0], BLUE[1], BLUE[2]);
    doc.rect(x, y, tableWidth, headerH, 'F');

    doc.setFillColor(LIGHT_GRAY[0], LIGHT_GRAY[1], LIGHT_GRAY[2]);
    doc.rect(x + colWidths[0] + colWidths[1], y + headerH + (visibleLines.length * rowH), colWidths[2] + colWidths[3], rowH, 'F');

    doc.setTextColor(255, 255, 255);
    doc.setFont('helvetica', 'bold');
    doc.setFontSize(10);
    doc.text('Cantidad', x + 6, y + 12);
    doc.text('Descripcion', x + colWidths[0] + 6, y + 12);
    doc.text('Precio Unitario', x + colWidths[0] + colWidths[1] + 6, y + 12);
    doc.text('Total', x + colWidths[0] + colWidths[1] + colWidths[2] + 6, y + 12);

    doc.setTextColor(0, 0, 0);
    doc.setFont('helvetica', 'normal');
    doc.setFontSize(9);

    visibleLines.forEach((line, index) => {
      const rowY = y + headerH + (index * rowH);
      doc.text(line.qty, x + 6, rowY + 11);

      const descX = x + colWidths[0] + 6;
      const descMaxWidth = colWidths[1] - 12;
      const descText = line.description || '';
      const wrapped = doc.splitTextToSize(descText, descMaxWidth);
      if (wrapped.length > 0) {
        doc.text(wrapped[0], descX, rowY + 11);
      }

      doc.text(line.unitPrice, x + colWidths[0] + colWidths[1] + colWidths[2] - 6, rowY + 11, { align: 'right' });
      doc.text(line.total, x + tableWidth - 6, rowY + 11, { align: 'right' });
    });

    const totalY = y + headerH + (visibleLines.length * rowH);
    doc.setFont('helvetica', 'bold');
    doc.setFontSize(10);
    doc.text('Total General', x + colWidths[0] + colWidths[1] + 30, totalY + 11);
    doc.text(money(subtotal), x + tableWidth - 6, totalY + 11, { align: 'right' });

    doc.setDrawColor(BORDER_GRAY[0], BORDER_GRAY[1], BORDER_GRAY[2]);
    doc.setLineWidth(0.5);

    const verticals = [
      x,
      x + colWidths[0],
      x + colWidths[0] + colWidths[1],
      x + colWidths[0] + colWidths[1] + colWidths[2],
      x + tableWidth
    ];

    verticals.forEach(vx => doc.line(vx, y, vx, y + tableH));

    doc.line(x, y, x + tableWidth, y);
    doc.line(x, y + headerH, x + tableWidth, y + headerH);

    for (let i = 1; i <= visibleLines.length; i++) {
      const rowLineY = y + headerH + (i * rowH);
      doc.line(x, rowLineY, x + tableWidth, rowLineY);
    }

    doc.line(x, y + tableH, x + tableWidth, y + tableH);

    return y + tableH;
  }

  function drawNotes(doc, startY) {
    doc.setTextColor(BLUE[0], BLUE[1], BLUE[2]);
    doc.setFont('helvetica', 'bold');
    doc.setFontSize(11);
    doc.text('Notas:', 46, startY);

    doc.setTextColor(0, 0, 0);
    doc.setFont('helvetica', 'normal');
    doc.setFontSize(10);
    doc.text('- Esta cotizacion es valida por 7 dias.', 46, startY + 17);
    doc.text('- Precios incluyen transporte.', 46, startY + 31);
  }

  async function generateQuotePdf(quote, options) {
    if (!window.jspdf || !window.jspdf.jsPDF) {
      throw new Error('jsPDF no esta disponible.');
    }

    const { jsPDF } = window.jspdf;
    const doc = new jsPDF({ orientation: 'portrait', unit: 'pt', format: 'a4' });

    const logoDataUrl = await loadLogoDataUrl();
    drawHeader(doc, quote, logoDataUrl);
    const tableBottom = drawQuoteTable(doc, quote);
    drawNotes(doc, tableBottom + 28);

    const safeName = (quote.name || 'cotizacion')
      .replace(/[^a-z0-9_-]+/gi, '_')
      .toLowerCase();

    const fileName = (options && options.fileName)
      || ('cotizacion_' + (quote.id || 'sin_id') + '_' + safeName + '.pdf');

    doc.save(fileName);
  }

  window.QuotePdf = {
    generateQuotePdf
  };
})();
