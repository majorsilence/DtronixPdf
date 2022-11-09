﻿using System;
using System.Threading;
using System.Threading.Tasks;
using DtronixCommon;
using DtronixCommon.Threading.Dispatcher;
using DtronixCommon.Threading.Dispatcher.Actions;
using DtronixPdf.Actions;
using PDFiumCore;
using static System.Collections.Specialized.BitVector32;

namespace DtronixPdf
{
    public partial class PdfPage : IDisposable
    {
        internal readonly PdfDocument Document;
        private readonly FpdfPageT _pageInstance;
        private bool _isDisposed = false;

        public float Width { get; private set; }
        public float Height { get; private set; }

        public int InitialIndex { get; private set; }

        internal FpdfPageT PageInstance => _pageInstance;

        private PdfPage(PdfDocument document, FpdfPageT pageInstance)
        {
            Document = document;
            _pageInstance = pageInstance;
        }

        internal static PdfPage Create(
            PdfDocument document,
            int pageIndex)
        {
            var loadPageResult = document.Synchronizer.SyncExec(() => fpdfview.FPDF_LoadPage(document.Instance, pageIndex));
            if (loadPageResult == null)
                throw new Exception($"Failed to open page for page index {pageIndex}.");

            var page = new PdfPage(document, loadPageResult)
            {
                InitialIndex = pageIndex
            };

            var getPageSizeResult = document.Synchronizer.SyncExec(() =>
            {
                var size = new FS_SIZEF_();

                var result = fpdfview.FPDF_GetPageSizeByIndexF(document.Instance, pageIndex, size);

                return result == 0 ? null : size;
            });

            if (getPageSizeResult == null)
                throw new Exception($"Could not retrieve page size for page index {pageIndex}.");
            page.Width = getPageSizeResult.Width;
            page.Height = getPageSizeResult.Height;

            return page;
        }

        public PdfBitmap Render(
            float scale,
            uint? argbBackground,
            RenderFlags flags = RenderFlags.RenderAnnotations,
            CancellationToken cancellationToken = default)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(PdfPage));

            var action = new RenderPageAction(
                Document.Synchronizer,
                PageInstance,
                scale,
                new Boundary(0, 0, Width, Height),
                flags,
                argbBackground,
                cancellationToken);

            return Document.Synchronizer.SyncExec(() => action.ExecuteSync(default));
        }

        public PdfBitmap Render(
            float scale,
            uint? argbBackground,
            Boundary viewport,
            RenderFlags flags = RenderFlags.RenderAnnotations,
            CancellationToken cancellationToken = default)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(PdfPage));

            if (viewport.IsEmpty)
                throw new ArgumentException("Viewport is empty", nameof(viewport));

            var action = new RenderPageAction(
                Document.Synchronizer,
                PageInstance,
                scale,
                viewport,
                flags,
                argbBackground,
                cancellationToken);

            return Document.Synchronizer.SyncExec(() => action.ExecuteSync(default));
        }

        public PdfBitmap Render(RenderPageAction action)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(PdfPage));

            return Document.Synchronizer.SyncExec(() => action.ExecuteSync(CancellationToken.None));

        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            Document.Synchronizer.SyncExec(() => fpdfview.FPDF_ClosePage(PageInstance));
        }
    }
}
