// ─── Catalog search ─────────────────────────────────────
function clearSearch() {
    document.getElementById("searchInput").value = "";
    document.getElementById("catalogSearch").submit();
}

// ─── Carousel ────────────────────────────────────────────
let _carouselIndex = 0;
let _carouselTimer = null;
const CAROUSEL_INTERVAL = 7000;

function carouselShow(index) {
    const slides = document.querySelectorAll('.carousel-slide');
    const dots   = document.querySelectorAll('.carousel-dot');
    if (!slides.length) return;

    _carouselIndex = (index + slides.length) % slides.length;

    slides.forEach((s, i) => s.classList.toggle('active', i === _carouselIndex));
    dots.forEach((d, i)   => d.classList.toggle('active', i === _carouselIndex));
}

function carouselNext() { carouselShow(_carouselIndex + 1); _resetTimer(); }
function carouselPrev() { carouselShow(_carouselIndex - 1); _resetTimer(); }
function carouselGo(i)  { carouselShow(i);                  _resetTimer(); }

function _resetTimer() {
    clearInterval(_carouselTimer);
    _carouselTimer = setInterval(() => carouselShow(_carouselIndex + 1), CAROUSEL_INTERVAL);
}

function _initCarousel() {
    if (!document.querySelector('.carousel')) return;
    _carouselIndex = 0;
    carouselShow(0);
    _resetTimer();
}

document.addEventListener('DOMContentLoaded', _initCarousel);
document.addEventListener('enhancedload',    _initCarousel);

// ─── Add-to-cart feedback ─────────────────────────────────
function _initAddToCart() {
    const savedY = sessionStorage.getItem('cart_scrollY');
    if (savedY !== null) {
        sessionStorage.removeItem('cart_scrollY');
        window.scrollTo(0, parseInt(savedY, 10));
    }

    document.querySelectorAll('.js-add-to-cart').forEach(btn => {
        if (btn.dataset.cartBound) return;
        btn.dataset.cartBound = '1';

        btn.addEventListener('click', function (e) {
            e.preventDefault();
            sessionStorage.setItem('cart_scrollY', window.scrollY);
            btn.textContent = '✓ Added!';
            btn.disabled = true;
            setTimeout(() => btn.closest('form').submit(), 750);
        });
    });
}

document.addEventListener('DOMContentLoaded', _initAddToCart);
document.addEventListener('enhancedload',    _initAddToCart);
