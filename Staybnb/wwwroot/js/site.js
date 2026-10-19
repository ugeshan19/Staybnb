(function () {
    // mark the sidebar link for the current page
    document.querySelectorAll('.dash-nav a').forEach(function (a) {
        var href = a.getAttribute('href');
        if (!href) return;
        var path = window.location.pathname.toLowerCase();
        var target = href.toLowerCase();
        if (path === target || (target.length > 1 && path.indexOf(target) === 0)) a.classList.add('active');
    });

    var toggle = document.getElementById('sidebarToggle');
    if (toggle) {
        toggle.addEventListener('click', function () {
            document.getElementById('dashWrapper').classList.toggle('collapsed');
        });
    }

    // booking widget: asks the server for a price breakdown whenever the dates change
    var widget = document.getElementById('bookingWidget');
    if (widget) {
        var money = function (n) { return 'R ' + Number(n).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 }); };
        var inEl = document.getElementById('CheckInDate');
        var outEl = document.getElementById('CheckOutDate');
        var summary = document.getElementById('quoteSummary');
        var btn = document.getElementById('bookBtn');
        var propertyId = widget.dataset.propertyId;

        var refresh = function () {
            if (!inEl.value || !outEl.value) return;
            fetch('/Properties/Quote?propertyId=' + propertyId + '&checkIn=' + inEl.value + '&checkOut=' + outEl.value)
                .then(function (r) { return r.json(); })
                .then(function (q) {
                    if (!q.valid) {
                        summary.innerHTML = '<div class="text-danger small">' + q.message + '</div>';
                        if (btn) btn.disabled = true;
                        return;
                    }
                    if (btn) btn.disabled = false;
                    summary.innerHTML =
                        '<div class="d-flex justify-content-between"><span>' + money(q.nightlyRate) + ' x ' + q.nights + ' night(s)</span><span>' + money(q.subtotal) + '</span></div>' +
                        '<div class="d-flex justify-content-between"><span>Cleaning fee</span><span>' + money(q.cleaningFee) + '</span></div>' +
                        '<div class="d-flex justify-content-between"><span>Service fee</span><span>' + money(q.serviceFee) + '</span></div>' +
                        '<hr class="my-2"/><div class="d-flex justify-content-between fw-bold"><span>Total</span><span>' + money(q.total) + '</span></div>';
                });
        };
        inEl.addEventListener('change', function () {
            if (outEl.value <= inEl.value) {
                var d = new Date(inEl.value); d.setDate(d.getDate() + 1);
                outEl.value = d.toISOString().slice(0, 10);
            }
            outEl.min = inEl.value;
            refresh();
        });
        outEl.addEventListener('change', refresh);
        refresh();
    }

    // thumbnails / file names under file inputs
    document.querySelectorAll('input[type=file][data-preview]').forEach(function (input) {
        input.addEventListener('change', function () {
            var box = document.getElementById(input.dataset.preview);
            if (!box) return;
            box.innerHTML = '';
            Array.prototype.forEach.call(input.files, function (f) {
                if (f.type.indexOf('image/') === 0) {
                    var img = document.createElement('img');
                    img.src = URL.createObjectURL(f);
                    img.className = 'thumb';
                    box.appendChild(img);
                } else {
                    var s = document.createElement('span');
                    s.className = 'badge text-bg-light border';
                    s.textContent = f.name;
                    box.appendChild(s);
                }
            });
        });
    });
    // back button: go to the previous page if it was on this site, otherwise fall back to the link (home)
    document.querySelectorAll('[data-back]').forEach(function (link) {
        link.addEventListener('click', function (e) {
            var fromThisSite = document.referrer && document.referrer.indexOf(window.location.origin) === 0;
            if (fromThisSite && window.history.length > 1) {
                e.preventDefault();
                window.history.back();
            }
        });
    });

    // photo viewer on the property page
    var photoModal = document.getElementById('photoModal');
    if (photoModal) {
        var carouselEl = document.getElementById('photoCarousel');
        var counter = document.getElementById('photoCounter');
        var items = carouselEl.querySelectorAll('.carousel-item');
        var thumbs = document.querySelectorAll('[data-photo-thumb]');

        var showInfo = function (index) {
            counter.textContent = (index + 1) + ' / ' + items.length;
            thumbs.forEach(function (t, i) {
                t.classList.toggle('active', i === index);
            });
            if (thumbs[index]) thumbs[index].scrollIntoView({ block: 'nearest', inline: 'center' });
        };

        // open on whichever photo was clicked
        photoModal.addEventListener('show.bs.modal', function (e) {
            var start = 0;
            if (e.relatedTarget && e.relatedTarget.dataset.photoIndex) {
                start = parseInt(e.relatedTarget.dataset.photoIndex, 10);
            }
            items.forEach(function (item, i) {
                item.classList.toggle('active', i === start);
            });
            showInfo(start);
        });

        carouselEl.addEventListener('slid.bs.carousel', function (e) {
            showInfo(e.to);
        });

        thumbs.forEach(function (t, i) {
            t.addEventListener('click', function () {
                bootstrap.Carousel.getOrCreateInstance(carouselEl).to(i);
            });
        });

        // left / right arrow keys while the viewer is open
        document.addEventListener('keydown', function (e) {
            if (!photoModal.classList.contains('show')) return;
            var carousel = bootstrap.Carousel.getOrCreateInstance(carouselEl);
            if (e.key === 'ArrowRight') carousel.next();
            if (e.key === 'ArrowLeft') carousel.prev();
        });
    }
})();
